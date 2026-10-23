using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

internal sealed class PausedGridLayer(
	IGridLayer<float> inner,
	ManualResetEventSlim entered,
	ManualResetEventSlim proceed
) : IGridLayer<float> {

	public GridLayout Layout => inner.Layout;

	public ref float this[int column, int row] => ref inner[column, row];

	public Span<float> GetValues(
		int chunkIndex
	) {
		entered.Set();
		if( !proceed.Wait( TimeSpan.FromSeconds( 10 ) ) ) {
			throw new TimeoutException( "The test did not resume the frame copy." );
		}
		return inner.GetValues( chunkIndex );
	}

	public Span<float> GetRowValues(
		int chunkIndex,
		int row
	) => inner.GetRowValues( chunkIndex, row );

	public void CopyPadded(
		int chunkIndex,
		int halo,
		Span<float> values
	) => inner.CopyPadded( chunkIndex, halo, values );

	public void CopyPaddedRow(
		int chunkIndex,
		int row,
		int halo,
		Span<float> values
	) => inner.CopyPaddedRow( chunkIndex, row, halo, values );

	public bool IsDirty(
		int chunkIndex
	) => inner.IsDirty( chunkIndex );

	public void MarkDirty(
		int chunkIndex
	) => inner.MarkDirty( chunkIndex );

	public void ClearDirty() => inner.ClearDirty();
}
