using System.Numerics;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity;

internal static class GridLayoutExtensions {

	public static bool TryGetPopulatedBounds(
		this GridLayout layout,
		out int column,
		out int row,
		out int width,
		out int height
	) {
		int left = int.MaxValue;
		int top = int.MaxValue;
		int right = int.MinValue;
		int bottom = int.MinValue;
		int words = layout.PresenceWordsPerRow;
		foreach( int chunk in layout.EnumerateChunks() ) {
			int originX = layout.GetChunkColumn( chunk ) << layout.ChunkShift;
			int originY = layout.GetChunkRow( chunk ) << layout.ChunkShift;
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			for( int localRow = 0; localRow < layout.ChunkSize; localRow++ ) {
				for( int word = 0; word < words; word++ ) {
					ulong bits = presence[( localRow * words ) + word];
					if( bits == 0 ) {
						continue;
					}
					int x = originX + ( word * 64 );
					left = Math.Min( left, x + BitOperations.TrailingZeroCount( bits ) );
					right = Math.Max( right, x + 63 - BitOperations.LeadingZeroCount( bits ) );
					top = Math.Min( top, originY + localRow );
					bottom = Math.Max( bottom, originY + localRow );
				}
			}
		}
		if( left > right ) {
			column = row = width = height = 0;
			return false;
		}
		column = left;
		row = top;
		width = checked( right - left + 1 );
		height = checked( bottom - top + 1 );
		return true;
	}

}
