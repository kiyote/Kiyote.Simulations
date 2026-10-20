using Kiyote.Geometry.Topology;

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
		DenseGridSource<float> source = new DenseGridSource<float>( Size * 4, Size * 4 );
		for( int row = 0; row < Size * 2; row++ ) {
			for( int column = 0; column < Size * 2; column++ ) {
				_ = source.TrySetCell( column, row, 0f );
			}
		}
		_ = source.TrySetCell( ( Size * 3 ) + 1, ( Size * 3 ) + 2, 0f );
		using ICompiledGridAssembly<float> compiled = TestGridAssembly.Compile( source );
		GridLayout layout = compiled.ChunkLayout;
		IGridLayer<float> layer = compiled.CreateLayer<float>();
		foreach( int chunk in layout.EnumerateChunks() ) {
			Span<float> values = layer.GetValues( chunk );
			for( int i = 0; i < values.Length; i++ ) {
				values[i] = ( ( chunk + 1 ) * 10000.0f ) + i;
			}
		}
		Assert.That( layout.TryGetChunk( chunkColumn, chunkRow, out int target ), Is.True );
		float[] expected = new float[AtmosphereSimd.PaddedArea];
		float[] actual = new float[AtmosphereSimd.PaddedArea];
		layer.CopyPadded( target, AtmosphereSimd.Halo, expected );

		AtmosphereSimd.Gather( layer, target, actual );

		Assert.That( actual, Is.EqualTo( expected ) );
	}

}
