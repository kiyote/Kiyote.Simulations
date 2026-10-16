using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

// A real ChunkedGridLayer<T> behind IGridLayer<T>, for code that reads layers rather than spans.
internal sealed class TestGridLayer<T> : IGridLayer<T> where T : unmanaged {

	private readonly HashSet<int> _dirty;

	public TestGridLayer(
		ChunkedGridLayout layout
	) {
		Values = new ChunkedGridLayer<T>( layout );
		_dirty = [];
	}

	public ChunkedGridLayout Layout => Values.Layout;

	public ChunkedGridLayer<T> Values { get; }

	public ref T this[int column, int row] => ref Values.GetReference( column, row );

	public bool IsDirty(
		int chunkIndex
	) {
		return _dirty.Contains( chunkIndex );
	}

	public void MarkDirty(
		int chunkIndex
	) {
		_dirty.Add( chunkIndex );
	}

	public void ClearDirty() {
		_dirty.Clear();
	}

	public void Fill(
		T value
	) {
		foreach( int chunk in Layout.EnumerateChunks() ) {
			Values.GetValues( chunk ).Fill( value );
		}
	}

}

internal static class TestGridLayout {

	// A layout with a width x height block of populated cells at the origin.
	public static ChunkedGridLayout Create(
		int width,
		int height
	) {
		ChunkedGridLayout layout = new ChunkedGridLayout( AtmosphereSimd.ChunkSize );
		for( int row = 0; row < height; row++ ) {
			_ = layout.SetPopulatedRun( 0, row, width );
		}
		return layout;
	}

}
