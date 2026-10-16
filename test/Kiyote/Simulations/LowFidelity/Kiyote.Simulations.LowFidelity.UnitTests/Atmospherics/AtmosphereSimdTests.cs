using Kiyote.Buffers.ChunkedGrids;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereSimdTests {

	[TestCase( 0, 0 )]
	[TestCase( 1, 1 )]
	[TestCase( 3, 3 )]
	public void Gather_MatchesCopyPadded(
		int chunkColumn,
		int chunkRow
	) {
		const int Size = AtmosphereSimd.ChunkSize;
		// A 2x2 block of chunks plus a lone chunk with no neighbours.
		ChunkedGridLayout layout = TestGridLayout.Create( Size * 2, Size * 2 );
		_ = layout.SetPopulated( ( Size * 3 ) + 1, ( Size * 3 ) + 2 );
		TestGridLayer<float> layer = new TestGridLayer<float>( layout );
		foreach( int chunk in layout.EnumerateChunks() ) {
			Span<float> values = layer.Values.GetValues( chunk );
			for( int i = 0; i < values.Length; i++ ) {
				values[i] = ( ( chunk + 1 ) * 10000.0f ) + i;
			}
		}
		Assert.That( layout.TryGetChunk( chunkColumn, chunkRow, out int target ), Is.True );
		float[] expected = new float[AtmosphereSimd.PaddedArea];
		float[] actual = new float[AtmosphereSimd.PaddedArea];
		layer.Values.GetNeighbourhood( target ).CopyPadded( AtmosphereSimd.Halo, expected );

		AtmosphereSimd.Gather( layer, target, actual );

		Assert.That( actual, Is.EqualTo( expected ) );
	}

}
