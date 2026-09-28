using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Projection;

// Pressure projection over a compiled topology, matching the boundary semantics of the
// non-vectorized GridProjection:
//   - An unflagged direction is a wall: velocity reflects, pressure is zero-gradient.
//   - A flagged direction with an in-leaf cell or a seam behind it is a neighbour.
//   - A flagged direction with nothing behind it is open: zero (Dirichlet) pressure and
//     zero-gradient outflow velocity.
// The caller-owned ProjectionNeighbourhood resolves the neighbour layout once per topology;
// the Jacobi solve over
// leaf interiors (where every neighbour is in-leaf) is vectorized.
public sealed class GridProjection : IGridProjection {

	// See the non-vectorized GridProjection for the derivation of this constant.
	private const float PoissonScale = 8.235294f;

	private const int Wall = ProjectionNeighbourhood<object>.Wall;
	private const int Outside = ProjectionNeighbourhood<object>.Outside;
	private const int DirectionCount = ProjectionNeighbourhood<object>.DirectionCount;
	private const float Diagonal = 0.7071068f;

	private static readonly float[] _unitX = [ 0f, Diagonal, 1f, Diagonal, 0f, -Diagonal, -1f, -Diagonal ];
	private static readonly float[] _unitY = [ -1f, -Diagonal, 0f, Diagonal, 1f, Diagonal, 0f, -Diagonal ];

	private readonly int _iterations;

	public GridProjection(
		IGridProjectionSettings settings
	) {
		_iterations = settings.Iterations;
	}

	void IGridProjection.Update<TCell>(
		ProjectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> sourceVelocityX,
		Field<TCell> sourceVelocityY,
		Field<TCell> destinationVelocityX,
		Field<TCell> destinationVelocityY,
		Field<TCell> pressure,
		Field<TCell> pressureScratch,
		Field<TCell> divergence
	) {
		GridTopology<TCell> topology = neighbourhood.Topology;
		ReadOnlySpan<int> neighbours = neighbourhood.Neighbours;
		ReadOnlySpan<Direction> cells = topology.Cells;
		ReadOnlySpan<float> vx = sourceVelocityX.Values;
		ReadOnlySpan<float> vy = sourceVelocityY.Values;
		Span<float> rhs = divergence.Values;

		CalculateDivergence( neighbours, cells, vx, vy, rhs );

		Span<float> relaxationSource = pressure.Values;
		Span<float> relaxationDestination = pressureScratch.Values;
		for( int i = 0; i < _iterations; i++ ) {
			Relax( topology, neighbours, cells, relaxationSource, relaxationDestination, rhs );
			Span<float> swap = relaxationSource;
			relaxationSource = relaxationDestination;
			relaxationDestination = swap;
		}
		if( ( _iterations & 1 ) == 1 ) {
			relaxationSource.CopyTo( pressure.Values );
		}

		SubtractGradient( neighbours, cells, pressure.Values, vx, vy, destinationVelocityX.Values, destinationVelocityY.Values );
	}

	private static void CalculateDivergence(
		ReadOnlySpan<int> neighbours,
		ReadOnlySpan<Direction> cells,
		ReadOnlySpan<float> vx,
		ReadOnlySpan<float> vy,
		Span<float> rhs
	) {
		for( int index = 0; index < rhs.Length; index++ ) {
			if( cells[index] == Direction.None ) {
				rhs[index] = 0f;
				continue;
			}
			float x = vx[index];
			float y = vy[index];
			float divergence = 0f;
			int baseIndex = index * DirectionCount;
			for( int d = 0; d < DirectionCount; d++ ) {
				int neighbour = neighbours[baseIndex + d];
				if( neighbour >= 0 ) {
					divergence += ( ( vx[neighbour] - x ) * _unitX[d] ) + ( ( vy[neighbour] - y ) * _unitY[d] );
				} else if( neighbour == Wall ) {
					// Reflected neighbour velocity: (reflected - self) . u = -2 (self . u)
					divergence -= 2f * ( ( x * _unitX[d] ) + ( y * _unitY[d] ) );
				}
				// Outside: zero-gradient outflow contributes nothing.
			}
			rhs[index] = PoissonScale * divergence / DirectionCount;
		}
	}

	private static void Relax<TCell>(
		GridTopology<TCell> topology,
		ReadOnlySpan<int> neighbours,
		ReadOnlySpan<Direction> cells,
		ReadOnlySpan<float> source,
		Span<float> destination,
		ReadOnlySpan<float> rhs
	) {
		ReadOnlySpan<byte> flagBytes = MemoryMarshal.AsBytes( cells );
		int lanes = Vector<float>.Count;
		Span<int> laneFlags = stackalloc int[lanes];

		foreach( TopologyLeaf<TCell> leaf in topology.Leaves ) {
			int width = leaf.Width;
			int height = leaf.Height;
			for( int row = 0; row < height; row++ ) {
				int rowStart = leaf.Offset + ( row * width );
				int column = 0;
				if( row > 0 && row < height - 1 && width > 2 ) {
					RelaxCell( neighbours, source, destination, rhs, rowStart );
					column = 1;
					for( ; column + lanes <= width - 1; column += lanes ) {
						int index = rowStart + column;
						for( int lane = 0; lane < lanes; lane++ ) {
							laneFlags[lane] = flagBytes[index + lane];
						}
						Vector<int> flags = new Vector<int>( laneFlags );
						Vector<float> sum = Vector<float>.Zero;
						Vector<float> count = Vector<float>.Zero;
						Accumulate( flags, Direction.North, source, index - width, ref sum, ref count );
						Accumulate( flags, Direction.NorthEast, source, index - width + 1, ref sum, ref count );
						Accumulate( flags, Direction.East, source, index + 1, ref sum, ref count );
						Accumulate( flags, Direction.SouthEast, source, index + width + 1, ref sum, ref count );
						Accumulate( flags, Direction.South, source, index + width, ref sum, ref count );
						Accumulate( flags, Direction.SouthWest, source, index + width - 1, ref sum, ref count );
						Accumulate( flags, Direction.West, source, index - 1, ref sum, ref count );
						Accumulate( flags, Direction.NorthWest, source, index - width - 1, ref sum, ref count );

						Vector<float> current = new Vector<float>( source[index..] );
						Vector<float> hasNeighbours = Vector.GreaterThan( count, Vector<float>.Zero ).As<int, float>();
						Vector<float> safeCount = Vector.Max( count, Vector<float>.One );
						Vector<float> updated = ( sum - new Vector<float>( rhs[index..] ) ) / safeCount;
						Vector.ConditionalSelect( hasNeighbours, updated, current ).CopyTo( destination[index..] );
					}
				}
				for( ; column < width; column++ ) {
					RelaxCell( neighbours, source, destination, rhs, rowStart + column );
				}
			}
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static void Accumulate(
		Vector<int> flags,
		Direction direction,
		ReadOnlySpan<float> source,
		int neighbour,
		ref Vector<float> sum,
		ref Vector<float> count
	) {
		Vector<int> bit = new Vector<int>( (int)direction );
		Vector<float> mask = Vector.Equals( flags & bit, bit ).As<int, float>();
		sum += Vector.ConditionalSelect( mask, new Vector<float>( source[neighbour..] ), Vector<float>.Zero );
		count += Vector.ConditionalSelect( mask, Vector<float>.One, Vector<float>.Zero );
	}

	private static void RelaxCell(
		ReadOnlySpan<int> neighbours,
		ReadOnlySpan<float> source,
		Span<float> destination,
		ReadOnlySpan<float> rhs,
		int index
	) {
		float sum = 0f;
		int count = 0;
		int baseIndex = index * DirectionCount;
		for( int d = 0; d < DirectionCount; d++ ) {
			int neighbour = neighbours[baseIndex + d];
			if( neighbour >= 0 ) {
				sum += source[neighbour];
				count++;
			} else if( neighbour == Outside ) {
				// Open edge to nothingness: fixed zero (Dirichlet) pressure.
				count++;
			}
		}
		destination[index] = count == 0 ? source[index] : ( sum - rhs[index] ) / count;
	}

	private static void SubtractGradient(
		ReadOnlySpan<int> neighbours,
		ReadOnlySpan<Direction> cells,
		ReadOnlySpan<float> pressure,
		ReadOnlySpan<float> vx,
		ReadOnlySpan<float> vy,
		Span<float> destinationX,
		Span<float> destinationY
	) {
		for( int index = 0; index < pressure.Length; index++ ) {
			if( cells[index] == Direction.None ) {
				destinationX[index] = vx[index];
				destinationY[index] = vy[index];
				continue;
			}
			float self = pressure[index];
			float gradientX = 0f;
			float gradientY = 0f;
			int baseIndex = index * DirectionCount;
			for( int d = 0; d < DirectionCount; d++ ) {
				int neighbour = neighbours[baseIndex + d];
				float delta;
				if( neighbour >= 0 ) {
					delta = pressure[neighbour] - self;
				} else if( neighbour == Outside ) {
					delta = -self;
				} else {
					// Wall: zero-gradient (Neumann) ghost pressure.
					continue;
				}
				gradientX += delta * _unitX[d];
				gradientY += delta * _unitY[d];
			}
			destinationX[index] = vx[index] - ( gradientX / DirectionCount );
			destinationY[index] = vy[index] - ( gradientY / DirectionCount );
		}
	}

}
