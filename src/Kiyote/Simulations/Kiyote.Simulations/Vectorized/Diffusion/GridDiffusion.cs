using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Diffusion;

// Explicit diffusion over a compiled topology:
//   destination[i] = source[i] + Rate * Σ (source[n] - source[i])
// over every open neighbour n. Neighbours within a leaf are resolved from the leaf's
// row-major layout; neighbours in other leaves come only from the topology's seams.
// A flag that points out of a leaf with no seam is treated as a wall.
public sealed class GridDiffusion : IGridDiffusion {

	private readonly float _rate;

	public GridDiffusion(
		IGridDiffusionSettings settings
	) {
		_rate = settings.Rate;
	}

	void IGridDiffusion.Update<TCell>(
		Field<TCell> source,
		Field<TCell> destination
	) {
		GridTopology<TCell> topology = source.Topology;
		ReadOnlySpan<float> input = source.Values;
		Span<float> output = destination.Values;
		ReadOnlySpan<Direction> cells = topology.Cells;
		float rate = _rate;

		ReadOnlySpan<byte> flagBytes = MemoryMarshal.AsBytes( cells );
		int lanes = Vector<float>.Count;
		Span<int> laneFlags = stackalloc int[lanes];

		foreach( TopologyLeaf<TCell> leaf in topology.Leaves ) {
			int width = leaf.Width;
			int height = leaf.Height;
			for( int row = 0; row < height; row++ ) {
				int rowStart = leaf.Offset + ( row * width );
				bool interiorRow = row > 0 && row < height - 1;
				int column = 0;
				if( interiorRow && width > 2 ) {
					ScalarCell( input, output, cells, rowStart, 0, row, width, height, rate );
					column = 1;
					// SIMD over the interior columns; every neighbour is in-leaf here.
					for( ; column + lanes <= width - 1; column += lanes ) {
						int index = rowStart + column;
						for( int lane = 0; lane < lanes; lane++ ) {
							laneFlags[lane] = flagBytes[index + lane];
						}
						Vector<int> flags = new Vector<int>( laneFlags );
						Vector<float> value = new Vector<float>( input[index..] );
						Vector<float> sum = Vector<float>.Zero;
						sum += GatherLanes( flags, Direction.North, input, index - width, value );
						sum += GatherLanes( flags, Direction.NorthEast, input, index - width + 1, value );
						sum += GatherLanes( flags, Direction.East, input, index + 1, value );
						sum += GatherLanes( flags, Direction.SouthEast, input, index + width + 1, value );
						sum += GatherLanes( flags, Direction.South, input, index + width, value );
						sum += GatherLanes( flags, Direction.SouthWest, input, index + width - 1, value );
						sum += GatherLanes( flags, Direction.West, input, index - 1, value );
						sum += GatherLanes( flags, Direction.NorthWest, input, index - width - 1, value );
						// Cells with Direction.None gather nothing, so they copy through unchanged.
						( value + ( sum * rate ) ).CopyTo( output[index..] );
					}
				}
				for( ; column < width; column++ ) {
					ScalarCell( input, output, cells, rowStart, column, row, width, height, rate );
				}
			}
		}

		// Seams are directed and listed from both sides, so each side gathers its own
		// share and the exchange stays conservative.
		foreach( SeamLink seam in topology.Seams ) {
			output[seam.Index] += rate * ( input[seam.NeighbourIndex] - input[seam.Index] );
		}
	}

	private static void ScalarCell(
		ReadOnlySpan<float> input,
		Span<float> output,
		ReadOnlySpan<Direction> cells,
		int rowStart,
		int column,
		int row,
		int width,
		int height,
		float rate
	) {
		int index = rowStart + column;
		float value = input[index];
		Direction flags = cells[index];
		if( flags == Direction.None ) {
			output[index] = value;
			return;
		}

		bool hasNorth = row > 0;
		bool hasSouth = row < height - 1;
		bool hasWest = column > 0;
		bool hasEast = column < width - 1;
		float sum = 0f;
		if( hasNorth ) {
			sum += Gather( flags, Direction.North, input, index - width, value );
			if( hasWest ) {
				sum += Gather( flags, Direction.NorthWest, input, index - width - 1, value );
			}
			if( hasEast ) {
				sum += Gather( flags, Direction.NorthEast, input, index - width + 1, value );
			}
		}
		if( hasSouth ) {
			sum += Gather( flags, Direction.South, input, index + width, value );
			if( hasWest ) {
				sum += Gather( flags, Direction.SouthWest, input, index + width - 1, value );
			}
			if( hasEast ) {
				sum += Gather( flags, Direction.SouthEast, input, index + width + 1, value );
			}
		}
		if( hasWest ) {
			sum += Gather( flags, Direction.West, input, index - 1, value );
		}
		if( hasEast ) {
			sum += Gather( flags, Direction.East, input, index + 1, value );
		}

		output[index] = value + ( rate * sum );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector<float> GatherLanes(
		Vector<int> flags,
		Direction direction,
		ReadOnlySpan<float> input,
		int neighbour,
		Vector<float> value
	) {
		Vector<int> bit = new Vector<int>( (int)direction );
		Vector<int> mask = Vector.Equals( flags & bit, bit );
		Vector<float> difference = new Vector<float>( input[neighbour..] ) - value;
		return Vector.ConditionalSelect( mask.As<int, float>(), difference, Vector<float>.Zero );
	}

	private static float Gather(
		Direction flags,
		Direction direction,
		ReadOnlySpan<float> input,
		int neighbour,
		float value
	) {
		return ( flags & direction ) != 0 ? input[neighbour] - value : 0f;
	}

}
