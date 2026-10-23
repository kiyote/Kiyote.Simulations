using Kiyote.Buffers.ChunkedGrids;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// A read-only view whose lifetime is bounded by its owning frame lease.
public readonly record struct SimulationFrameLayer {

	private readonly ChunkedGridLayer<float> _layer;

	internal SimulationFrameLayer(
		ChunkedGridLayer<float> layer
	) {
		_layer = layer;
	}

	public int ChunkSize => _layer.ChunkSize;

	public int ChunkCount => _layer.Layout.ChunkCount;

	public float this[int column, int row] => _layer.GetReference( column, row );

	public bool TryGet(
		int column,
		int row,
		out float value
	) {
		return _layer.TryGet( column, row, out value );
	}

	public bool IsPopulated(
		int column,
		int row
	) {
		return _layer.Layout.IsPopulated( column, row );
	}

	public ChunkIndexEnumerator EnumerateChunks() {
		return _layer.Layout.EnumerateChunks();
	}

	public int GetChunkColumn(
		int chunk
	) {
		return _layer.Layout.GetChunkColumn( chunk );
	}

	public int GetChunkRow(
		int chunk
	) {
		return _layer.Layout.GetChunkRow( chunk );
	}

	public ReadOnlySpan<float> GetValues(
		int chunk
	) {
		return _layer.GetValues( chunk );
	}

	public ReadOnlySpan<ulong> GetPresence(
		int chunk
	) {
		return _layer.Layout.GetPresence( chunk );
	}

}
