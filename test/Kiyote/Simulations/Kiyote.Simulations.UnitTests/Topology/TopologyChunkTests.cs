using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.UnitTests;

[TestFixture( 32, 0, false )]
[TestFixture( 32, -2, true )]
[TestFixture( 64, 0, false )]
[TestFixture( 64, -2, true )]
public sealed class TopologyChunkTests {

	private readonly int _chunkSize;
	private readonly int _chunkOffset;
	private readonly bool _partial;

	private ICompiledGridAssembly<float> _compiled;
	private GridLayout _layout;

	private int Stride => _chunkSize + 2;

	public TopologyChunkTests( int chunkSize, int chunkOffset, bool partial ) {
		_chunkSize = chunkSize;
		_chunkOffset = chunkOffset;
		_partial = partial;
	}

	[SetUp]
	public void SetUp() {
		DenseGridSource<float> source = new DenseGridSource<float>( _chunkSize * 7, _chunkSize * 7 );
		for( int row = 0; row < _chunkSize * 3; row++ ) {
			for( int column = 0; column < _chunkSize * 3; column++ ) {
				if( !_partial || ( column % 7 != 3 && row % 5 != 2 ) ) {
					Assert.That( source.TrySetCell( column, row, 0f ), Is.True );
				}
			}
		}
		Assert.That( source.TrySetCell( ( _chunkSize * 4 ) + 1, ( _chunkSize * 4 ) + 2, 0f ), Is.True );
		Assert.That( source.TrySetCell( _chunkSize * 6, _chunkSize * 6, 0f ), Is.True );
		IGridAssembly<float> assembly = new GridAssembly<float>();
		AttachResult attached = assembly.TryAttach( source, _chunkOffset * _chunkSize, _chunkOffset * _chunkSize );
		Assert.That( attached.Succeeded, Is.True );
		_compiled = new GridCompiler().Compile( assembly, _chunkSize );
		_layout = _compiled.ChunkLayout;
		Assert.That( assembly.TryRemoveCell( attached.Placement, _chunkSize * 6, _chunkSize * 6 ), Is.True );
	}

	[TearDown]
	public void TearDown() {
		_compiled.Dispose();
	}

	[TestCase( 0, 0 )]
	[TestCase( 1, 0 )]
	[TestCase( 0, 1 )]
	[TestCase( 1, 1 )]
	[TestCase( 2, 2 )]
	[TestCase( 4, 4 )]
	[TestCase( 6, 6 )]
	public void GatherPadded_Float_MatchesCopyPadded(
		int chunkColumn,
		int chunkRow
	) {
		IGridLayer<float> layer = _compiled.CreateLayer<float>();
		Fill( layer, ( index, chunk ) => ( chunk * 10000.0f ) + index );

		AssertMatches( layer, chunkColumn, chunkRow, -1.0f );
	}

	[TestCase( 0, 0 )]
	[TestCase( 1, 0 )]
	[TestCase( 0, 1 )]
	[TestCase( 1, 1 )]
	[TestCase( 2, 2 )]
	[TestCase( 4, 4 )]
	[TestCase( 6, 6 )]
	public void GatherPadded_Direction_MatchesCopyPadded(
		int chunkColumn,
		int chunkRow
	) {
		IGridLayer<Direction> layer = _compiled.CreateLayer<Direction>();
		Fill( layer, ( index, chunk ) => (Direction)( ( index + chunk ) & 0xFF ) );

		AssertMatches( layer, chunkColumn, chunkRow, (Direction)255 );
	}

	[Test]
	public void GatherPadded_OversizedScratch_LeavesTheRestUntouched() {
		IGridLayer<float> layer = _compiled.CreateLayer<float>();
		Assert.That( _layout.TryGetChunk( _chunkOffset, _chunkOffset, out int chunk ), Is.True );
		float[] scratch = new float[( Stride * Stride ) + 4];
		Array.Fill( scratch, -1.0f );

		TopologyChunk.GatherPadded( layer, chunk, scratch );

		Assert.That( scratch[^4..], Is.All.EqualTo( -1.0f ) );
	}

	[Test]
	public void Bits_ReadsTheRequestedColumns() {
		ulong[] presence = new ulong[_chunkSize];
		presence[3] = 0b1011_0000UL;

		using( Assert.EnterMultipleScope() ) {
			Assert.That( TopologyChunk.Bits( presence, 1, 3, 4, 4 ), Is.EqualTo( 0b1011UL ) );
			Assert.That( TopologyChunk.Bits( presence, 1, 3, 8, 8 ), Is.Zero );
			Assert.That( TopologyChunk.AnyInRow( presence, 1, 3 ), Is.True );
			Assert.That( TopologyChunk.AnyInRow( presence, 1, 2 ), Is.False );
		}
	}

	[TestCase( 1, 1 )]
	[TestCase( 4, 4 )]
	public void GatherPadded_WarmedFloatAndDirectionPaths_AllocateNoManagedBytes( int chunkColumn, int chunkRow ) {
		IGridLayer<float> values = _compiled.CreateLayer<float>();
		IGridLayer<Direction> directions = _compiled.CreateLayer<Direction>();
		Assert.That( _layout.TryGetChunk( chunkColumn + _chunkOffset, chunkRow + _chunkOffset, out int chunk ), Is.True );
		float[] paddedValues = new float[Stride * Stride];
		Direction[] paddedDirections = new Direction[Stride * Stride];
		for( int i = 0; i < 100; i++ ) {
			GatherBoth( values, directions, chunk, paddedValues, paddedDirections );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int i = 0; i < 100; i++ ) {
			GatherBoth( values, directions, chunk, paddedValues, paddedDirections );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private static void GatherBoth(
		IGridLayer<float> values,
		IGridLayer<Direction> directions,
		int chunk,
		float[] paddedValues,
		Direction[] paddedDirections
	) {
		TopologyChunk.GatherPadded( values, chunk, paddedValues );
		TopologyChunk.GatherPadded( directions, chunk, paddedDirections );
		values.CopyPadded( chunk, 1, paddedValues );
		directions.CopyPadded( chunk, 1, paddedDirections );
	}

	private void AssertMatches<T>(
		IGridLayer<T> layer,
		int chunkColumn,
		int chunkRow,
		T sentinel
	) where T : unmanaged {
		chunkColumn += _chunkOffset;
		chunkRow += _chunkOffset;
		Assert.That( _layout.TryGetChunk( chunkColumn, chunkRow, out int chunk ), Is.True );
		T[] expected = new T[( Stride * Stride ) + 4];
		T[] copied = new T[expected.Length];
		T[] actual = new T[expected.Length];
		Array.Fill( expected, sentinel );
		Array.Fill( copied, sentinel );
		Array.Fill( actual, sentinel );
		for( int row = 0; row < Stride; row++ ) {
			for( int column = 0; column < Stride; column++ ) {
				int worldColumn = ( chunkColumn * _chunkSize ) + column - 1;
				int worldRow = ( chunkRow * _chunkSize ) + row - 1;
				expected[( row * Stride ) + column] = _layout.TryLocate( worldColumn, worldRow, out int source, out int local )
					? layer.GetValues( source )[local]
					: default;
			}
		}

		layer.CopyPadded( chunk, 1, copied );
		TopologyChunk.GatherPadded( layer, chunk, actual );

		Assert.That( copied, Is.EqualTo( expected ) );
		Assert.That( actual, Is.EqualTo( expected ) );
	}

	private void Fill<T>(
		IGridLayer<T> layer,
		Func<int, int, T> value
	) where T : unmanaged {
		foreach( int chunk in _layout.EnumerateChunks() ) {
			Span<T> values = layer.GetValues( chunk );
			ReadOnlySpan<ulong> presence = _layout.GetPresence( chunk );
			for( int i = 0; i < values.Length; i++ ) {
				int row = i / _chunkSize;
				int column = i % _chunkSize;
				if( ( presence[( row * _layout.PresenceWordsPerRow ) + ( column >> 6 )] & ( 1UL << ( column & 63 ) ) ) != 0 ) {
					values[i] = value( i, chunk + 1 );
				}
			}
		}
	}

}
