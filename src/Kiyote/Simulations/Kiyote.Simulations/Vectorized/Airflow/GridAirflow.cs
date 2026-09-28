using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Vectorized.Advection;
using Kiyote.Simulations.Vectorized.Pressure;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Vectorized.Airflow;

public sealed class GridAirflow : IGridAirflow {

	// How strongly the species' pressure gradient accelerates the bulk velocity.
	private const float PressureForce = 0.05f;

	private const int North = 0;
	private const int East = 2;
	private const int South = 4;
	private const int West = 6;

	private readonly IGridPressure _pressure;
	private readonly IGridProjection _projection;
	private readonly IGridAdvection _advection;
	private readonly float _forceScale;

	public GridAirflow(
		IGridPressure pressure,
		IGridProjection projection,
		IGridAdvection advection,
		ISimulationClock clock
	) {
		_pressure = pressure;
		_projection = projection;
		_advection = advection;
		_forceScale = clock.FixedTimeStep * PressureForce * 0.5f;
	}

	void IGridAirflow.Update<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> pressureSource,
		Field<TCell> pressureDestination,
		Field<TCell> sourceVelocityX,
		Field<TCell> sourceVelocityY,
		Field<TCell> destinationVelocityX,
		Field<TCell> destinationVelocityY,
		Field<TCell> intermediateVelocityX,
		Field<TCell> intermediateVelocityY,
		Field<TCell> projectionPressure,
		Field<TCell> projectionPressureScratch,
		Field<TCell> concentrationSource,
		Field<TCell> concentrationDestination
	) {
		_pressure.Update( pressureSource, pressureDestination );

		// Carry the velocity field along itself so jets travel away from their source.
		_advection.Update( neighbourhood, sourceVelocityX, sourceVelocityY, sourceVelocityX, intermediateVelocityX );
		_advection.Update( neighbourhood, sourceVelocityX, sourceVelocityY, sourceVelocityY, intermediateVelocityY );

		ApplyPressureForce( neighbourhood, pressureDestination.Values, intermediateVelocityX.Values, intermediateVelocityY.Values );

		_projection.Update(
			intermediateVelocityX,
			intermediateVelocityY,
			destinationVelocityX,
			destinationVelocityY,
			projectionPressure,
			projectionPressureScratch
		);

		_advection.Update( neighbourhood, destinationVelocityX, destinationVelocityY, concentrationSource, concentrationDestination );
	}

	// Subtracts dt * PressureForce * grad(p) using central differences. A closed edge is
	// a wall (zero gradient across it); an open edge with no available cell behind it
	// is open to zero ambient pressure. Seams are followed via the neighbourhood.
	private void ApplyPressureForce<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		ReadOnlySpan<float> pressure,
		Span<float> velocityX,
		Span<float> velocityY
	) {
		ReadOnlySpan<int> neighbours = neighbourhood.Neighbours;
		ReadOnlySpan<Direction> cells = neighbourhood.Topology.Cells;
		float scale = _forceScale;

		for( int index = 0; index < pressure.Length; index++ ) {
			Direction flags = cells[index];
			if( flags == Direction.None ) {
				continue;
			}
			float self = pressure[index];
			int baseIndex = index * AdvectionNeighbourhood<TCell>.DirectionCount;
			float east = Neighbour( flags, Direction.East, neighbours[baseIndex + East], self, pressure );
			float west = Neighbour( flags, Direction.West, neighbours[baseIndex + West], self, pressure );
			float south = Neighbour( flags, Direction.South, neighbours[baseIndex + South], self, pressure );
			float north = Neighbour( flags, Direction.North, neighbours[baseIndex + North], self, pressure );

			velocityX[index] -= ( east - west ) * scale;
			velocityY[index] -= ( south - north ) * scale;
		}
	}

	private static float Neighbour(
		Direction flags,
		Direction direction,
		int neighbour,
		float self,
		ReadOnlySpan<float> pressure
	) {
		if( ( flags & direction ) == 0 ) {
			return self;
		}
		return neighbour >= 0 ? pressure[neighbour] : 0f;
	}

}
