using System.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Couples IGridPressure's partial-pressure fields to IGridFluid's velocity
// engine: pressure differences across passable neighbor cells are converted
// into a Velocity impulse (a local -deltaP body force), applied to the grid's
// velocity component, then IGridFluid.StepVelocity and AdvectGases carry that
// impulse into actual bulk airflow.
public sealed class GridAirflow : IGridAirflow {

	private static readonly (int DeltaColumn, int DeltaRow)[] _edgeDeltas = [
		( 1, 0 ),
		( 0, 1 ),
		( 1, 1 ),
		( 1, -1 ),
	];

	private readonly IGridFluid _gridFluid;
	private readonly IGridPressure _gridPressure;

	public GridAirflow(
		IGridFluid gridFluid,
		IGridPressure gridPressure
	) {
		_gridFluid = gridFluid;
		_gridPressure = gridPressure;
	}

	void IGridAirflow.ApplyPressureForcing<TCell, TVelocityAccessor, TForcingStrategy, TBoundary, TCellSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IGrid<double> totalPressure,
		TForcingStrategy forcingStrategy,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );
		ArgumentNullException.ThrowIfNull( totalPressure );

		int left = grid.Column;
		int top = grid.Row;
		int width = grid.Width;
		int height = grid.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		Velocity[] deltas = ArrayPool<Velocity>.Shared.Rent( size );
		try {
			Array.Clear( deltas, 0, size );

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int sourceIndex = column - left + ( ( row - top ) * width );
					GridCell<double> source = new( column, row, totalPressure[column, row] );

					foreach( (int deltaColumn, int deltaRow) in _edgeDeltas ) {
						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
							|| boundary.IsSolid( neighborColumn, neighborRow )
						) {
							continue;
						}

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );
						GridCell<double> destination = new( neighborColumn, neighborRow, totalPressure[neighborColumn, neighborRow] );

						Velocity force = forcingStrategy.CalculateForce( source, destination );

						// Unlike a conserved diffusion transfer (which is negated for the
						// destination so what leaves one cell arrives at the other), a
						// pressure-gradient body force pushes both cells the same
						// direction - from high pressure toward low pressure - so both
						// source and destination accumulate the same force here.
						deltas[sourceIndex] = forcingStrategy.Combine( deltas[sourceIndex], force );
						deltas[destinationIndex] = forcingStrategy.Combine( deltas[destinationIndex], force );
					}
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int index = column - left + ( ( row - top ) * width );
					Velocity delta = deltas[index];
					if( delta == Velocity.Zero ) {
						continue;
					}

					TCell current = grid[column, row]!;
					GridCell<TCell> cell = new( column, row, current );
					Velocity updated = Velocity.Add( velocityAccessor.GetVelocity( current ), Velocity.Multiply( delta, timeStep ) );
					TCell withVelocity = velocityAccessor.WithVelocity( current, updated );
					setCell.Callback( cell, withVelocity );
				}
			}
		} finally {
			ArrayPool<Velocity>.Shared.Return( deltas );
		}
	}

	void IGridAirflow.Advance<TCell, TVelocityAccessor, TForcingStrategy, TVelocityDiffusion, TScalar, TSampler, TBoundary, TCellSetCell, TScalarSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IGrid<double> totalPressure,
		TForcingStrategy forcingStrategy,
		TVelocityDiffusion velocityDiffusion,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TSampler sampler,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );
		ArgumentNullException.ThrowIfNull( gases );

		IGridAirflow self = this;
		self.ApplyPressureForcing<TCell, TVelocityAccessor, TForcingStrategy, TBoundary, TCellSetCell>(
			grid,
			velocityAccessor,
			totalPressure,
			forcingStrategy,
			boundary,
			setCell,
			timeStep
		);

		_gridFluid.StepVelocity<TCell, TVelocityAccessor, TVelocityDiffusion, TSampler, TBoundary, TCellSetCell>(
			grid,
			velocityAccessor,
			velocityDiffusion,
			sampler,
			boundary,
			setCell,
			timeStep
		);

		_gridFluid.AdvectGases<TCell, TVelocityAccessor, TScalar, TSampler, TBoundary, TScalarSetCell>(
			grid,
			velocityAccessor,
			gases,
			sampler,
			boundary,
			timeStep
		);
	}

}
