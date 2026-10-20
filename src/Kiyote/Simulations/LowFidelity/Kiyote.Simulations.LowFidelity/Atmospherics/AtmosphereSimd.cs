using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Shared constants and SIMD helpers for the low fidelity stages.
//
// Stage kernels work on one chunk at a time through two span shapes:
//   - chunk spans hold ChunkSize * ChunkSize values, row-major, as returned by
//     IGridLayer<T>.GetValues; cell (c, r) is at ChunkIndex( c, r ).
//   - padded spans hold the chunk plus a one-cell border from its neighbours, as written by
//     IGridLayer<T>.CopyPadded( chunk, 1, ... ); cell (c, r) is at PaddedIndex( c, r ),
//     so its west neighbour is at -1 and its north neighbour at -PaddedStride.
// Kernels only read neighbours through padded spans.
internal static class AtmosphereSimd {

	public const int ChunkSize = 32;
	public const int ChunkArea = ChunkSize * ChunkSize;
	public const int Halo = 1;
	public const int PaddedStride = ChunkSize + ( 2 * Halo );
	public const int PaddedArea = PaddedStride * PaddedStride;
	public const int Lanes = 8;
	public const uint FullRow = uint.MaxValue;
	public const float ReferenceTemperature = 293.15f;
	public const float Epsilon = 1e-4f;

	// Presence bits for one chunk row; bit n is column n.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static uint GetRowMask(
		ReadOnlySpan<ulong> presence,
		int row
	) {
		return (uint)presence[row];
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static int ChunkIndex(
		int column,
		int row
	) {
		return ( row * ChunkSize ) + column;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static int PaddedIndex(
		int column,
		int row
	) {
		return ( ( row + Halo ) * PaddedStride ) + column + Halo;
	}

	// Gathers a chunk and its one-cell border into padded scratch, writing default where a
	// neighbouring chunk is not allocated. Equivalent to IGridLayer<T>.CopyPadded( chunk, 1, ... ),
	// but copies each row once and the border cells individually, which is much cheaper for a halo of 1.
	public static void Gather<T>(
		IGridLayer<T> layer,
		int chunk,
		Span<T> padded
	) where T : unmanaged {
		IGridLayer<T> values = layer;
		ReadOnlySpan<int> neighbours = layer.Layout.GetNeighbours( chunk );
		ReadOnlySpan<T> centre = values.GetValues( chunk );
		ReadOnlySpan<T> west = GetValues( values, neighbours[3] );
		ReadOnlySpan<T> east = GetValues( values, neighbours[5] );
		padded = padded[..PaddedArea];

		for( int row = 0; row < ChunkSize; row++ ) {
			int start = ( row + Halo ) * PaddedStride;
			int source = row * ChunkSize;
			centre.Slice( source, ChunkSize ).CopyTo( padded.Slice( start + Halo, ChunkSize ) );
			padded[start] = west.IsEmpty ? default : west[source + ChunkSize - 1];
			padded[start + ChunkSize + 1] = east.IsEmpty ? default : east[source];
		}
		GatherEdgeRow( values, neighbours, 0, ChunkSize - 1, padded[..PaddedStride] );
		GatherEdgeRow( values, neighbours, 6, 0, padded.Slice( ( ChunkSize + 1 ) * PaddedStride, PaddedStride ) );
	}

	// Fills the row above (band 0, source row ChunkSize - 1) or below (band 6, source row 0) the chunk.
	private static void GatherEdgeRow<T>(
		IGridLayer<T> values,
		ReadOnlySpan<int> neighbours,
		int band,
		int sourceRow,
		Span<T> destination
	) where T : unmanaged {
		int source = sourceRow * ChunkSize;
		ReadOnlySpan<T> left = GetValues( values, neighbours[band] );
		ReadOnlySpan<T> middle = GetValues( values, neighbours[band + 1] );
		ReadOnlySpan<T> right = GetValues( values, neighbours[band + 2] );
		destination[0] = left.IsEmpty ? default : left[source + ChunkSize - 1];
		destination[ChunkSize + 1] = right.IsEmpty ? default : right[source];
		if( middle.IsEmpty ) {
			destination.Slice( 1, ChunkSize ).Clear();
		} else {
			middle.Slice( source, ChunkSize ).CopyTo( destination.Slice( 1, ChunkSize ) );
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static ReadOnlySpan<T> GetValues<T>(
		IGridLayer<T> values,
		int chunk
	) where T : unmanaged {
		return chunk < 0 ? [] : values.GetValues( chunk );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> Load(
		ref float source,
		int index
	) {
		return Vector256.LoadUnsafe( ref source, (nuint)index );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void Store(
		Vector256<float> value,
		ref float destination,
		int index
	) {
		value.StoreUnsafe( ref destination, (nuint)index );
	}

	// Widens 8 consecutive byte flags into one int lane each.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<int> LoadFlags(
		ref byte flags,
		int index
	) {
		Vector128<byte> packed = Vector128.CreateScalarUnsafe( Unsafe.ReadUnaligned<ulong>( ref Unsafe.Add( ref flags, index ) ) ).AsByte();
		(Vector128<uint> low, Vector128<uint> high) = Vector128.Widen( Vector128.WidenLower( packed ) );
		return Vector256.Create( low, high ).AsInt32();
	}

	// Per-lane mask where the given bit is set in the widened flags.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> HasFlag(
		Vector256<int> flags,
		Direction bit
	) {
		return ( ~Vector256.Equals( flags & Vector256.Create( (int)bit ), Vector256<int>.Zero ) ).AsSingle();
	}

	// Per-lane mask where the byte at each of 8 consecutive positions is non-zero.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> IsSet(
		ref byte values,
		int index
	) {
		return ( ~Vector256.Equals( LoadFlags( ref values, index ), Vector256<int>.Zero ) ).AsSingle();
	}

	// True when no presence word of the chunk is set.
	public static bool IsEmpty(
		GridLayout layout,
		int chunk
	) {
		return layout.GetFill( chunk ) == ChunkFill.Empty;
	}

}
