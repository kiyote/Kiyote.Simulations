using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Diffusion;

// Explicit diffusion over a chunked layer:
//   destination[i] = source[i] + Rate * Σ (source[n] - source[i])
// over every neighbour n flagged in the neighbourhood (passable or vacuum). Unoccupied
// cells always hold 0, so a vacuum neighbour drains Rate * source[i], which is discarded.
// Unflagged neighbours are walls and contribute nothing. Unoccupied cells have no
// directions and hold 0, so they are written back as 0 without any masking.
// Each chunk's source values are gathered with a one-cell border from its neighbouring
// chunks into padded scratch; missing chunks pad with 0.
public sealed class GridDiffusion : IGridDiffusion {

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

	// Cells processed per SIMD block: one 128-bit vector of direction bytes, two 256-bit vectors of floats.
	private const int Block = 16;
	// Smallest positive normal single; anything smaller is flushed to zero.
	private const float MinNormal = 1.17549435E-38f;
	private const int Halo = 1;

	private readonly float _rate;

	public GridDiffusion(
		IGridDiffusionSettings settings
	) {
		_rate = settings.Rate;
	}

	IGridLayer<Direction> IGridDiffusion.CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	) {
		IGridLayer<Direction> vacuum = compiled.CreateVacuumLayer();
		IGridLayer<Direction> neighbourhood = compiled.CreateLayer<Direction>();
		GridLayout layout = compiled.ChunkLayout;
		foreach( int chunk in layout.EnumerateChunks() ) {
			ReadOnlySpan<Direction> passable = connectivity.GetValues( chunk );
			ReadOnlySpan<Direction> missing = vacuum.GetValues( chunk );
			Span<Direction> combined = neighbourhood.GetValues( chunk );
			for( int i = 0; i < combined.Length; i++ ) {
				combined[i] = passable[i] | missing[i];
			}
		}
		compiled.RemoveLayer( vacuum );
		return neighbourhood;
	}

	void IGridDiffusion.Update(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> source,
		IGridLayer<float> destination
	) {
		GridLayout layout = source.Layout;
		int size = layout.ChunkSize;
		int inputStride = size + ( 2 * Halo );
		float rate = _rate;
		Vector256<float> minNormal = Vector256.Create( MinNormal );
		Span<int> inputOffsets = stackalloc int[_directions.Length];
		for( int d = 0; d < _directions.Length; d++ ) {
			inputOffsets[d] = ( _deltaRows[d] * inputStride ) + _deltaColumns[d];
		}

		IGridLayer<Direction> flagValues = neighbourhood;
		IGridLayer<float> sourceValues = source;
		IGridLayer<float> destinationValues = destination;
		float[] scratch = ArrayPool<float>.Shared.Rent( ChunkPadding.GetPaddedLength( size, Halo ) );
		try {
			foreach( int chunk in layout.EnumerateChunks() ) {
				if( layout.GetFill( chunk ) == ChunkFill.Empty ) {
					continue;
				}
				TopologyChunk.GatherPadded( sourceValues, chunk, scratch );
				ReadOnlySpan<Direction> flags = flagValues.GetValues( chunk );
				ReadOnlySpan<float> input = scratch;
				Span<float> output = destinationValues.GetValues( chunk );
				ref byte flagsRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( flags ) );
				ref float inputRef = ref MemoryMarshal.GetReference( input );
				ref float outputRef = ref MemoryMarshal.GetReference( output );

				ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
				int words = layout.PresenceWordsPerRow;
				for( int row = 0; row < size; row++ ) {
					// Unoccupied cells have no directions and hold 0, so their result is always 0; skip them.
					if( !TopologyChunk.AnyInRow( presence, words, row ) ) {
						continue;
					}
					int flagStart = row * size;
					int inputStart = ( ( row + Halo ) * inputStride ) + Halo;
					int outputStart = row * size;

					int column = 0;
					for( ; column + Block <= size; column += Block ) {
						if( TopologyChunk.Bits( presence, words, row, column, Block ) == 0 ) {
							continue;
						}
						int i = inputStart + column;
						Vector128<byte> include = Vector128.LoadUnsafe( ref flagsRef, (nuint)( flagStart + column ) );
						Vector256<float> value0 = Vector256.LoadUnsafe( ref inputRef, (nuint)i );
						Vector256<float> value1 = Vector256.LoadUnsafe( ref inputRef, (nuint)( i + 8 ) );
						Vector256<float> sum0 = Vector256<float>.Zero;
						Vector256<float> sum1 = Vector256<float>.Zero;
						(Vector128<ushort> includeLow, Vector128<ushort> includeHigh) = Vector128.Widen( include );
						(Vector128<uint> a, Vector128<uint> b) = Vector128.Widen( includeLow );
						(Vector128<uint> c, Vector128<uint> e) = Vector128.Widen( includeHigh );
						Vector256<int> include0 = Vector256.Create( a, b ).AsInt32();
						Vector256<int> include1 = Vector256.Create( c, e ).AsInt32();
						for( int d = 0; d < _directions.Length; d++ ) {
							Vector256<int> bit = Vector256.Create( (int)_directions[d] );
							Vector256<float> mask0 = ( ~Vector256.Equals( include0 & bit, Vector256<int>.Zero ) ).AsSingle();
							Vector256<float> mask1 = ( ~Vector256.Equals( include1 & bit, Vector256<int>.Zero ) ).AsSingle();
							int n = i + inputOffsets[d];
							sum0 += ( Vector256.LoadUnsafe( ref inputRef, (nuint)n ) - value0 ) & mask0;
							sum1 += ( Vector256.LoadUnsafe( ref inputRef, (nuint)( n + 8 ) ) - value1 ) & mask1;
						}
						Vector256<float> result0 = value0 + ( sum0 * rate );
						Vector256<float> result1 = value1 + ( sum1 * rate );
						// Flush denormals to zero; values draining into vacuum otherwise decay into the denormal range, which is very slow on x86.
						( result0 & Vector256.GreaterThanOrEqual( Vector256.Abs( result0 ), minNormal ) ).StoreUnsafe( ref outputRef, (nuint)( outputStart + column ) );
						( result1 & Vector256.GreaterThanOrEqual( Vector256.Abs( result1 ), minNormal ) ).StoreUnsafe( ref outputRef, (nuint)( outputStart + column + 8 ) );
					}
					for( ; column < size; column++ ) {
						int i = inputStart + column;
						Direction cell = flags[flagStart + column];
						float value = input[i];
						float sum = 0f;
						for( int d = 0; d < _directions.Length; d++ ) {
							if( ( cell & _directions[d] ) != 0 ) {
								sum += input[i + inputOffsets[d]] - value;
							}
						}
						float result = value + ( rate * sum );
						output[outputStart + column] = MathF.Abs( result ) >= MinNormal ? result : 0f;
					}
				}
				destination.MarkDirty( chunk );
			}
		} finally {
			ArrayPool<float>.Shared.Return( scratch );
		}
	}

}
