using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Advection;
using Kiyote.Simulations.Pressure;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Airflow;

public sealed class GridAirflow : IGridAirflow {

	// How strongly the species' pressure gradient accelerates the bulk velocity.
	private const float PressureForce = 0.05f;

	private readonly IGridPressure _gridPressure;
	private readonly IGridProjection _gridProjection;
	private readonly IGridAdvection _gridAdvection;
	private readonly ISimulationClock _clock;
	private readonly VelocityBilinearSampler _velocitySampler;
	private RaggedArrayGrid<Velocity>? _intermediateVelocity;

	public GridAirflow(
		IGridPressure gridPressure,
		IGridProjection gridProjection,
		IGridAdvection gridAdvection,
		ISimulationClock clock
	) {
		_gridPressure = gridPressure;
		_gridProjection = gridProjection;
		_gridAdvection = gridAdvection;
		_clock = clock;
		_velocitySampler = new VelocityBilinearSampler();
	}

	void IGridAirflow.Update<TCell, TPressure, TFlow, TValue, TPressureStrategy, TProjectionStrategy, TSampler>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TPressure> pressureSource,
		IMutableGrid<TPressure> pressureDestination,
		TPressureStrategy pressureStrategy,
		IGrid<Velocity> sourceVelocity,
		IMutableGrid<Velocity> destinationVelocity,
		IMutableGrid<TPressure> projectionPressureSource,
		IMutableGrid<TPressure> projectionPressureDestination,
		TProjectionStrategy projectionStrategy,
		IGrid<TValue> concentrationSource,
		IMutableGrid<TValue> concentrationDestination,
		TSampler sampler
	) {
		// Relax this species' own pressure field forward by one fixed time step.
		_gridPressure.Update<TCell, TPressure, TFlow, TPressureStrategy>(
			connectivity,
			pressureSource,
			pressureDestination,
			pressureStrategy
		);

		// Carry the velocity field along itself so jets travel away from their source
		// rather than only being smoothed in place.
		IMutableGrid<Velocity> intermediate = GetIntermediateVelocity( sourceVelocity );
		_gridAdvection.Update<TCell, Velocity, VelocityBilinearSampler>(
			connectivity,
			sourceVelocity,
			sourceVelocity,
			intermediate,
			_velocitySampler
		);

		// Accelerate the air down the species' pressure gradient, so high pressure
		// pushes air outward and a sealed region pushes back as it fills.
		ApplyPressureForce( connectivity, pressureDestination, projectionStrategy, intermediate );

		// Project the shared bulk velocity field onto its divergence-free component,
		// using the scratch pressure grids as the projection's own relaxation buffers
		// (independent of the species' pressure field above).
		_gridProjection.Update<TCell, TPressure, TProjectionStrategy>(
			connectivity,
			intermediate,
			destinationVelocity,
			projectionPressureSource,
			projectionPressureDestination,
			projectionStrategy
		);

		// Advect this species' concentration along the resulting divergence-free
		// velocity field.
		_gridAdvection.Update<TCell, TValue, TSampler>(
			connectivity,
			destinationVelocity,
			concentrationSource,
			concentrationDestination,
			sampler
		);
	}

	private IMutableGrid<Velocity> GetIntermediateVelocity(
		IGrid<Velocity> source
	) {
		RaggedArrayGrid<Velocity>? intermediate = _intermediateVelocity;
		IGrid<Velocity>? existing = intermediate;
		if( existing is null
			|| existing.Column != source.Column
			|| existing.Row != source.Row
			|| existing.Width != source.Width
			|| existing.Height != source.Height
		) {
			intermediate = new RaggedArrayGrid<Velocity>( source.Column, source.Row, source.Width, source.Height );
			_intermediateVelocity = intermediate;
		}
		return intermediate!;
	}

	// Subtracts dt * PressureForce * grad(p) from velocity using central differences.
	// An unconnected edge is a wall (zero gradient across it); a connected edge that
	// leaves the grid is open to zero ambient pressure.
	private void ApplyPressureForce<TCell, TPressure, TProjectionStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TPressure> pressure,
		TProjectionStrategy strategy,
		IMutableGrid<Velocity> velocity
	)
		where TProjectionStrategy : IProjectionStrategy<TPressure> {
		int left = pressure.Column;
		int top = pressure.Row;
		int right = left + pressure.Width;
		int bottom = top + pressure.Height;
		float scale = _clock.FixedTimeStep * PressureForce * 0.5f;

		for( int row = top; row < bottom; row++ ) {
			for( int column = left; column < right; column++ ) {
				Direction flags = connectivity[column, row];
				if( flags == Direction.None ) {
					continue;
				}
				float self = strategy.GetPressure( pressure[column, row]! );
				float east = Neighbor( flags, Direction.East, column + 1, row );
				float west = Neighbor( flags, Direction.West, column - 1, row );
				float south = Neighbor( flags, Direction.South, column, row + 1 );
				float north = Neighbor( flags, Direction.North, column, row - 1 );

				Velocity current = velocity[column, row];
				velocity[column, row] = new Velocity(
					current.X - ( ( east - west ) * scale ),
					current.Y - ( ( south - north ) * scale )
				);

				float Neighbor( Direction cellFlags, Direction direction, int neighborColumn, int neighborRow ) {
					if( !cellFlags.HasFlag( direction ) ) {
						return self;
					}
					if( neighborColumn < left || neighborColumn >= right || neighborRow < top || neighborRow >= bottom ) {
						return 0f;
					}
					return strategy.GetPressure( pressure[neighborColumn, neighborRow]! );
				}
			}
		}
	}

}
