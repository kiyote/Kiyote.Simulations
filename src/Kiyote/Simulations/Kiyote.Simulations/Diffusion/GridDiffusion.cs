using System.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Diffusion;

public sealed class GridDiffusion : IGridDiffusion {

	private static readonly (int DeltaColumn, int DeltaRow)[] _edgeDeltas = [
		( 1, 0 ),
		( 0, 1 ),
		( 1, 1 ),
		( 1, -1 ),
	];

	void IGridDiffusion.Update<TCell, TFlow, TFlowStrategy, TPassability, TSetCell>(
		IGrid<TCell> grid,
		TFlowStrategy strategy,
		TPassability isPassable,
		TSetCell setCell,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );

		int left = grid.Column;
		int top = grid.Row;
		int width = grid.Width;
		int height = grid.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		TFlow[] deltas = ArrayPool<TFlow>.Shared.Rent( size );
		int[] neighborCounts = ArrayPool<int>.Shared.Rent( size );
		try {
			Array.Clear( deltas, 0, size );
			Array.Clear( neighborCounts, 0, size );

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					GridCell<TCell> source = new( column, row, grid[column, row] );

					if( !isPassable.Evaluate( source ) ) {
						continue;
					}

					int sourceIndex = column - left + ( ( row - top ) * width );

					foreach( (int deltaColumn, int deltaRow) in _edgeDeltas ) {
						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
						) {
							continue;
						}

						if( !IsReachable<TCell, TPassability>( grid, isPassable, column, row, deltaColumn, deltaRow, left, top, width, height ) ) {
							continue;
						}

						GridCell<TCell> destination = new( neighborColumn, neighborRow, grid[neighborColumn, neighborRow] );

						if( !isPassable.Evaluate( destination ) ) {
							continue;
						}

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );
						neighborCounts[sourceIndex]++;
						neighborCounts[destinationIndex]++;
					}
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					GridCell<TCell> source = new( column, row, grid[column, row] );

					if( !isPassable.Evaluate( source ) ) {
						continue;
					}

					int sourceIndex = column - left + ( ( row - top ) * width );

					foreach( (int deltaColumn, int deltaRow) in _edgeDeltas ) {
						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
						) {
							continue;
						}

						if( !IsReachable<TCell, TPassability>( grid, isPassable, column, row, deltaColumn, deltaRow, left, top, width, height ) ) {
							continue;
						}

						GridCell<TCell> destination = new( neighborColumn, neighborRow, grid[neighborColumn, neighborRow] );

						if( !isPassable.Evaluate( destination ) ) {
							continue;
						}

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );

						TFlow transfer = strategy.CalculateTransfer( source, destination, neighborCounts[sourceIndex], neighborCounts[destinationIndex], timeStep );

						deltas[sourceIndex] = strategy.Combine( deltas[sourceIndex], strategy.Negate( transfer ) );
						deltas[destinationIndex] = strategy.Combine( deltas[destinationIndex], transfer );
					}
				}
			}

			EqualityComparer<TFlow> comparer = EqualityComparer<TFlow>.Default;
			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					int index = column - left + ( ( row - top ) * width );
					TFlow delta = deltas[index];
					if( comparer.Equals( delta, default ) ) {
						continue;
					}

					GridCell<TCell> cell = new( column, row, grid[column, row] );
					TCell updated = strategy.Apply( cell, delta );
					setCell.Callback( cell, updated );
				}
			}
		} finally {
			ArrayPool<TFlow>.Shared.Return( deltas );
			ArrayPool<int>.Shared.Return( neighborCounts );
		}
	}

	private static bool IsReachable<TCell, TPassability>(
		IGrid<TCell> grid,
		TPassability isPassable,
		int column,
		int row,
		int deltaColumn,
		int deltaRow,
		int left,
		int top,
		int width,
		int height
	)
		where TPassability : ICellStrategy<TCell, bool> {
		if( deltaColumn == 0 || deltaRow == 0 ) {
			// Orthogonal neighbor, no diagonal corners to check.
			return true;
		}

		int orthogonalColumn = column + deltaColumn;
		int orthogonalRow = row + deltaRow;

		if( orthogonalColumn < left
			|| orthogonalColumn >= left + width
			|| row < top
			|| row >= top + height
		) {
			return false;
		}

		GridCell<TCell> horizontalEdge = new( orthogonalColumn, row, grid[orthogonalColumn, row] );
		if( !isPassable.Evaluate( horizontalEdge ) ) {
			return false;
		}

		if( column < left
			|| column >= left + width
			|| orthogonalRow < top
			|| orthogonalRow >= top + height
		) {
			return false;
		}

		GridCell<TCell> verticalEdge = new( column, orthogonalRow, grid[column, orthogonalRow] );
		if( !isPassable.Evaluate( verticalEdge ) ) {
			return false;
		}

		return true;
	}

}
