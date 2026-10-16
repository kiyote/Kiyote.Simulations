using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.UnitTests;

[TestFixture]
public sealed class TopologyChunkTests {

	private const int ChunkSize = 32;
	private const int Stride = ChunkSize + 2;

	private ChunkedGridLayout _layout;

	[SetUp]
	public void SetUp() {
		// Chunks (0,0), (1,0), (0,1) and (1,1) plus a lone chunk at (3,3); (2,*) and (*,2) are missing.
		_layout = new ChunkedGridLayout( ChunkSize );
		for( int row = 0; row < ChunkSize * 2; row++ ) {
			_ = _layout.SetPopulatedRun( 0, row, ChunkSize * 2 );
		}
		_ = _layout.SetPopulated( ( ChunkSize * 3 ) + 5, ( ChunkSize * 3 ) + 7 );
	}

	[TestCase( 0, 0 )]
	[TestCase( 1, 0 )]
	[TestCase( 0, 1 )]
	[TestCase( 1, 1 )]
	[TestCase( 3, 3 )]
	public void GatherPadded_Float_MatchesCopyPadded(
		int chunkColumn,
		int chunkRow
	) {
		ChunkedGridLayer<float> layer = new ChunkedGridLayer<float>( _layout );
		Fill( layer, ( index, chunk ) => ( chunk * 10000.0f ) + index );

		AssertMatches( layer, chunkColumn, chunkRow );
	}

	[TestCase( 0, 0 )]
	[TestCase( 1, 1 )]
	[TestCase( 3, 3 )]
	public void GatherPadded_Direction_MatchesCopyPadded(
		int chunkColumn,
		int chunkRow
	) {
		ChunkedGridLayer<Direction> layer = new ChunkedGridLayer<Direction>( _layout );
		Fill( layer, ( index, chunk ) => (Direction)( ( index + chunk ) & 0xFF ) );

		AssertMatches( layer, chunkColumn, chunkRow );
	}

	[Test]
	public void GatherPadded_OversizedScratch_LeavesTheRestUntouched() {
		ChunkedGridLayer<float> layer = new ChunkedGridLayer<float>( _layout );
		Assert.That( _layout.TryGetChunk( 0, 0, out int chunk ), Is.True );
		float[] scratch = new float[( Stride * Stride ) + 4];
		Array.Fill( scratch, -1.0f );

		TopologyChunk.GatherPadded( layer, chunk, scratch );

		Assert.That( scratch[^4..], Is.All.EqualTo( -1.0f ) );
	}

	[Test]
	public void Bits_ReadsTheRequestedColumns() {
		ulong[] presence = new ulong[ChunkSize];
		presence[3] = 0b1011_0000UL;

		using( Assert.EnterMultipleScope() ) {
			Assert.That( TopologyChunk.Bits( presence, 1, 3, 4, 4 ), Is.EqualTo( 0b1011UL ) );
			Assert.That( TopologyChunk.Bits( presence, 1, 3, 8, 8 ), Is.Zero );
			Assert.That( TopologyChunk.AnyInRow( presence, 1, 3 ), Is.True );
			Assert.That( TopologyChunk.AnyInRow( presence, 1, 2 ), Is.False );
		}
	}

	private void AssertMatches<T>(
		ChunkedGridLayer<T> layer,
		int chunkColumn,
		int chunkRow
	) where T : unmanaged {
		Assert.That( _layout.TryGetChunk( chunkColumn, chunkRow, out int chunk ), Is.True );
		T[] expected = new T[Stride * Stride];
		T[] actual = new T[Stride * Stride];
		layer.GetNeighbourhood( chunk ).CopyPadded( 1, expected );

		TopologyChunk.GatherPadded( layer, chunk, actual );

		Assert.That( actual, Is.EqualTo( expected ) );
	}

	private void Fill<T>(
		ChunkedGridLayer<T> layer,
		Func<int, int, T> value
	) where T : unmanaged {
		foreach( int chunk in _layout.EnumerateChunks() ) {
			Span<T> values = layer.GetValues( chunk );
			for( int i = 0; i < values.Length; i++ ) {
				values[i] = value( i, chunk + 1 );
			}
		}
	}

}
