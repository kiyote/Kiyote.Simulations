using System.Buffers;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Diffusion;

public sealed class GridDiffusion : IGridDiffusion {

	private static readonly (int DeltaColumn, int DeltaRow, Direction Direction)[] _edgeDeltas = [
		( 1, 0, Direction.East ),
		( 0, 1, Direction.South ),
		( 1, 1, Direction.SouthEast ),
		( 1, -1, Direction.NorthEast ),
	];

	void IGridDiffusion.Update<TCell, TValue, TFlow, TFlowStrategy>(
		IGrid<TValue> source,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<TValue> destination,
		TFlowStrategy flow
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( connectivity );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( flow );
		if( source.Width != destination.Width
			|| source.Height != destination.Height
		) {
			throw new ArgumentException( "Source and destination grids must have the same dimensions." );
		}

		int left = source.Column;
		int top = source.Row;
		int width = source.Width;
		int height = source.Height;
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
					int sourceIndex = column - left + ( ( row - top ) * width );
					Direction sourceConnectivity = connectivity[column, row];

					foreach( (int deltaColumn, int deltaRow, Direction direction) in _edgeDeltas ) {
						if( !sourceConnectivity.HasFlag( direction ) ) {
							continue;
						}

						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
						) {
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
					GridCell<TValue> sourceCell = new( column, row, source[column, row] );
					int sourceIndex = column - left + ( ( row - top ) * width );
					Direction sourceConnectivity = connectivity[column, row];

					foreach( (int deltaColumn, int deltaRow, Direction direction) in _edgeDeltas ) {
						if( !sourceConnectivity.HasFlag( direction ) ) {
							continue;
						}

						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
						) {
							continue;
						}

						GridCell<TValue> destinationCell = new( neighborColumn, neighborRow, source[neighborColumn, neighborRow] );

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );

						TFlow transfer = flow.CalculateTransfer( sourceCell, destinationCell, neighborCounts[sourceIndex], neighborCounts[destinationIndex] );

						deltas[sourceIndex] = flow.Combine( deltas[sourceIndex], flow.Negate( transfer ) );
						deltas[destinationIndex] = flow.Combine( deltas[destinationIndex], transfer );
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

					GridCell<TValue> cell = new( column, row, source[column, row] );
					TValue updated = flow.Apply( cell, delta );
					destination[cell.Column, cell.Row] = updated;
				}
			}
		} finally {
			ArrayPool<TFlow>.Shared.Return( deltas );
			ArrayPool<int>.Shared.Return( neighborCounts );
		}
	}

}
