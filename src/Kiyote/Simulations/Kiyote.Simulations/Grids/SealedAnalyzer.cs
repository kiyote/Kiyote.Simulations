using System.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public sealed class SealedAnalyzer : ISealedAnalyzer {

	private static readonly (int DeltaColumn, int DeltaRow)[] _neighborDeltas = [
		( -1, 0 ),
		( 1, 0 ),
		( 0, -1 ),
		( 0, 1 ),
		( -1, -1 ),
		( 1, -1 ),
		( -1, 1 ),
		( 1, 1 ),
	];

	bool ISealedAnalyzer.IsSealed<TCell, TPassability>(
		IGrid<TCell> grid,
		int startColumn,
		int startRow,
		TPassability isPassable
	) {
		ArgumentNullException.ThrowIfNull( grid );

		int left = grid.Column;
		int top = grid.Row;
		int width = grid.Width;
		int height = grid.Height;
		int size = width * height;

		bool[] visited = size > 0 ? ArrayPool<bool>.Shared.Rent( size ) : [];
		(int Column, int Row)[] queue = ArrayPool<(int Column, int Row)>.Shared.Rent( size > 0 ? size : 1 );
		try {
			if( size > 0 ) {
				Array.Clear( visited, 0, size );
			}

			int head = 0;
			int tail = 0;
			int count = 0;
			Enqueue( ref queue, ref head, ref tail, ref count, ( startColumn, startRow ) );

			if( startColumn >= left && startColumn < left + width
				&& startRow >= top && startRow < top + height
			) {
				visited[ startColumn - left  + ( ( startRow - top ) * width )] = true;
			}

			while( count > 0 ) {
				(int column, int row) = Dequeue( queue, ref head, ref count );

				if( column < left
					|| column >= left + width
					|| row < top
					|| row >= top + height
				) {
					return false;
				}

				TCell? cell = grid[column, row];
				if( !isPassable.Evaluate( new GridCell<TCell>( column, row, cell ) ) ) {
					continue;
				}

				foreach( (int deltaColumn, int deltaRow) in _neighborDeltas ) {
					int neighborColumn = column + deltaColumn;
					int neighborRow = row + deltaRow;

					if( neighborColumn < left
						|| neighborColumn >= left + width
						|| neighborRow < top
						|| neighborRow >= top + height
					) {
						Enqueue( ref queue, ref head, ref tail, ref count, ( neighborColumn, neighborRow ) );
						continue;
					}

					int index =  neighborColumn - left  + ( ( neighborRow - top ) * width );
					if( !visited[index] ) {
						visited[index] = true;
						Enqueue( ref queue, ref head, ref tail, ref count, ( neighborColumn, neighborRow ) );
					}
				}
			}

			return true;
		} finally {
			if( size > 0 ) {
				ArrayPool<bool>.Shared.Return( visited );
			}
			ArrayPool<(int Column, int Row)>.Shared.Return( queue );
		}
	}

	private static void Enqueue(
		ref (int Column, int Row)[] queue,
		ref int head,
		ref int tail,
		ref int count,
		(int Column, int Row) item
	) {
		if( count == queue.Length ) {
			(int Column, int Row)[] grown = ArrayPool<(int Column, int Row)>.Shared.Rent( queue.Length * 2 );
			for( int i = 0; i < count; i++ ) {
				grown[i] = queue[( head + i ) % queue.Length];
			}
			ArrayPool<(int Column, int Row)>.Shared.Return( queue );
			queue = grown;
			head = 0;
			tail = count;
		}

		queue[tail] = item;
		tail = ( tail + 1 ) % queue.Length;
		count++;
	}

	private static (int Column, int Row) Dequeue(
		(int Column, int Row)[] queue,
		ref int head,
		ref int count
	) {
		(int Column, int Row) item = queue[head];
		head = ( head + 1 ) % queue.Length;
		count--;
		return item;
	}

}
