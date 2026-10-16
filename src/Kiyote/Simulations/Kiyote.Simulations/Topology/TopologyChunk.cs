using System.Runtime.CompilerServices;
using Kiyote.Buffers.ChunkedGrids;

namespace Kiyote.Simulations.Topology;

// Helpers shared by the Topology kernels for working on one chunk of a ChunkedGridLayout.
internal static class TopologyChunk {

	// True when any cell of a chunk row is present. presence is ChunkedGridLayout.GetPresence( chunk ).
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static bool AnyInRow(
		ReadOnlySpan<ulong> presence,
		int words,
		int row
	) {
		return words == 1
			? presence[row] != 0
			: presence.Slice( row * words, words ).ContainsAnyExcept( 0UL );
	}

	// The presence bits of count (at most 63) cells starting at column of a chunk row, which
	// must not straddle a 64-cell word. presence is ChunkedGridLayout.GetPresence( chunk ).
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static ulong Bits(
		ReadOnlySpan<ulong> presence,
		int words,
		int row,
		int column,
		int count
	) {
		return ( presence[( row * words ) + ( column >> 6 )] >> ( column & 63 ) ) & ( ( 1UL << count ) - 1 );
	}

	// Writes the chunk and a one-cell border from its neighbours into padded, row-major with
	// stride ChunkSize + 2, writing default where a neighbouring chunk is not allocated.
	// Equivalent to GridChunkNeighbourhood<T>.CopyPadded( 1, padded ), but copies each row
	// once and the border cells individually, which is considerably cheaper for a halo of 1.
	public static void GatherPadded<T>(
		ChunkedGridLayer<T> layer,
		int chunk,
		Span<T> padded
	) where T : unmanaged {
		ChunkedGridLayout layout = layer.Layout;
		int size = layout.ChunkSize;
		int stride = size + 2;
		ReadOnlySpan<int> neighbours = layout.GetNeighbours( chunk );
		ReadOnlySpan<T> centre = layer.GetValues( chunk );
		ReadOnlySpan<T> west = Get( layer, neighbours[3] );
		ReadOnlySpan<T> east = Get( layer, neighbours[5] );
		padded = padded[..( stride * stride )];

		for( int row = 0; row < size; row++ ) {
			int start = ( row + 1 ) * stride;
			int source = row * size;
			centre.Slice( source, size ).CopyTo( padded.Slice( start + 1, size ) );
			padded[start] = west.IsEmpty ? default : west[source + size - 1];
			padded[start + size + 1] = east.IsEmpty ? default : east[source];
		}
		GatherEdgeRow( layer, neighbours, 0, size - 1, size, padded[..stride] );
		GatherEdgeRow( layer, neighbours, 6, 0, size, padded.Slice( ( size + 1 ) * stride, stride ) );
	}

	// Fills the row above (band 0, source row size - 1) or below (band 6, source row 0) the chunk.
	private static void GatherEdgeRow<T>(
		ChunkedGridLayer<T> layer,
		ReadOnlySpan<int> neighbours,
		int band,
		int sourceRow,
		int size,
		Span<T> destination
	) where T : unmanaged {
		int source = sourceRow * size;
		ReadOnlySpan<T> left = Get( layer, neighbours[band] );
		ReadOnlySpan<T> middle = Get( layer, neighbours[band + 1] );
		ReadOnlySpan<T> right = Get( layer, neighbours[band + 2] );
		destination[0] = left.IsEmpty ? default : left[source + size - 1];
		destination[size + 1] = right.IsEmpty ? default : right[source];
		if( middle.IsEmpty ) {
			destination.Slice( 1, size ).Clear();
		} else {
			middle.Slice( source, size ).CopyTo( destination.Slice( 1, size ) );
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static ReadOnlySpan<T> Get<T>(
		ChunkedGridLayer<T> layer,
		int chunk
	) where T : unmanaged {
		return chunk < 0 ? [] : layer.GetValues( chunk );
	}

}
