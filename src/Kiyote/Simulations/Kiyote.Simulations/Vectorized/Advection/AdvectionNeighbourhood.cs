using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Advection;

// Caller-owned lookup of each cell's eight neighbours over a compiled topology, used
// by GridAdvection to sample across seams. Build once per topology and reuse it until
// the topology changes. A neighbour slot is Unavailable when the direction is closed,
// falls outside every leaf, or leads to a wall cell (one with no open directions).
public sealed class AdvectionNeighbourhood<TCell> {

	public const int Unavailable = -1;
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

	public AdvectionNeighbourhood(
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
						int neighbourColumn = column + _deltaColumns[d];
						int neighbourRow = row + _deltaRows[d];
						neighbours[slot] = ( flags & _directions[d] ) != 0
							&& neighbourColumn >= 0 && neighbourColumn < leaf.Width
							&& neighbourRow >= 0 && neighbourRow < leaf.Height
							? leaf.Offset + ( neighbourRow * leaf.Width ) + neighbourColumn
							: Unavailable;
					}
				}
			}
		}

		foreach( SeamLink seam in topology.Seams ) {
			int d = Array.IndexOf( _directions, seam.Direction );
			if( d < 0 || ( cells[seam.Index] & seam.Direction ) == 0 ) {
				continue;
			}
			int slot = ( seam.Index * DirectionCount ) + d;
			if( neighbours[slot] == Unavailable ) {
				neighbours[slot] = seam.NeighbourIndex;
			}
		}

		for( int slot = 0; slot < neighbours.Length; slot++ ) {
			int neighbour = neighbours[slot];
			if( neighbour >= 0 && cells[neighbour] == Direction.None ) {
				neighbours[slot] = Unavailable;
			}
		}

		return neighbours;
	}
}
