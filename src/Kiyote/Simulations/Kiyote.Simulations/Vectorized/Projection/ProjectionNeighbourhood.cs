using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Projection;

// Caller-owned lookup of each cell's eight neighbours over a compiled topology, used
// by GridProjection. Build once per topology and reuse it until the topology changes.
// A neighbour slot is Wall when the direction is closed, Outside when the direction is
// open but no cell (in-leaf or across a seam) lies behind it, and otherwise the index
// of the neighbouring cell.
public sealed class ProjectionNeighbourhood<TCell> {

	public const int Wall = -1;
	public const int Outside = -2;
	public const int DirectionCount = 8;

	// Slot order matches Direction bit order: N, NE, E, SE, S, SW, W, NW.
	private static readonly Direction[] _directions = [
		Direction.North,
		Direction.NorthEast,
		Direction.East,
		Direction.SouthEast,
		Direction.South,
		Direction.SouthWest,
		Direction.West,
		Direction.NorthWest
	];
	private static readonly int[] _deltaColumns = [ 0, 1, 1, 1, 0, -1, -1, -1 ];
	private static readonly int[] _deltaRows = [ -1, -1, 0, 1, 1, 1, 0, -1 ];

	private readonly int[] _neighbours;

	public ProjectionNeighbourhood(
		GridTopology<TCell> topology
	) {
		Topology = topology;
		_neighbours = Build( topology );
	}

	public GridTopology<TCell> Topology { get; }

	public ReadOnlySpan<int> Neighbours => _neighbours;

	private static int[] Build(
		GridTopology<TCell> topology
	) {
		int[] neighbours = new int[topology.CellCount * DirectionCount];
		ReadOnlySpan<Direction> cells = topology.Cells;
		foreach( TopologyLeaf<TCell> leaf in topology.Leaves ) {
			for( int row = 0; row < leaf.Height; row++ ) {
				for( int column = 0; column < leaf.Width; column++ ) {
					int index = leaf.Offset + ( row * leaf.Width ) + column;
					Direction flags = cells[index];
					for( int d = 0; d < DirectionCount; d++ ) {
						int slot = ( index * DirectionCount ) + d;
						if( ( flags & _directions[d] ) == 0 ) {
							neighbours[slot] = Wall;
							continue;
						}
						int neighbourColumn = column + _deltaColumns[d];
						int neighbourRow = row + _deltaRows[d];
						neighbours[slot] = neighbourColumn >= 0 && neighbourColumn < leaf.Width && neighbourRow >= 0 && neighbourRow < leaf.Height
							? leaf.Offset + ( neighbourRow * leaf.Width ) + neighbourColumn
							: Outside;
					}
				}
			}
		}

		foreach( SeamLink seam in topology.Seams ) {
			int d = Array.IndexOf( _directions, seam.Direction );
			if( d < 0 ) {
				continue;
			}
			int slot = ( seam.Index * DirectionCount ) + d;
			if( neighbours[slot] == Outside ) {
				neighbours[slot] = seam.NeighbourIndex;
			}
		}

		return neighbours;
	}
}
