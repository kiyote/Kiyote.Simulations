using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture( 0, false )]
[TestFixture( -2, true )]
public sealed class AtmosphereSimdTests {

	private readonly int _chunkOffset;
	private readonly bool _partial;
	private ICompiledGridAssembly<float> _compiled;

	public AtmosphereSimdTests( int chunkOffset, bool partial ) {
		_chunkOffset = chunkOffset;
		_partial = partial;
	}

	[SetUp]
	public void SetUp() {
		const int Size = AtmosphereSimd.ChunkSize;
		DenseGridSource<float> source = new DenseGridSource<float>( Size * 7, Size * 7 );
		for( int row = 0; row < Size * 3; row++ ) {
			for( int column = 0; column < Size * 3; column++ ) {
				if( !_partial || ( column % 7 != 3 && row % 5 != 2 ) ) {
					Assert.That( source.TrySetCell( column, row, 0f ), Is.True );
				}
			}
		}
		Assert.That( source.TrySetCell( ( Size * 4 ) + 1, ( Size * 4 ) + 2, 0f ), Is.True );
		Assert.That( source.TrySetCell( Size * 6, Size * 6, 0f ), Is.True );
		IGridAssembly<float> assembly = new GridAssembly<float>();
		AttachResult attached = assembly.TryAttach( source, _chunkOffset * Size, _chunkOffset * Size );
		Assert.That( attached.Succeeded, Is.True );
		_compiled = new GridCompiler().Compile( assembly, Size );
		Assert.That( assembly.TryRemoveCell( attached.Placement, Size * 6, Size * 6 ), Is.True );
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
	public void Gather_Float_MatchesCopyPadded(
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
	public void Gather_Direction_MatchesCopyPadded( int chunkColumn, int chunkRow ) {
		IGridLayer<Direction> layer = _compiled.CreateLayer<Direction>();
		Fill( layer, ( index, chunk ) => (Direction)( ( index + chunk ) & 0xFF ) );

		AssertMatches( layer, chunkColumn, chunkRow, (Direction)255 );
	}

	[TestCase( 1, 1 )]
	[TestCase( 4, 4 )]
	public void Gather_WarmedFloatAndDirectionPaths_AllocateNoManagedBytes( int chunkColumn, int chunkRow ) {
		IGridLayer<float> values = _compiled.CreateLayer<float>();
		IGridLayer<Direction> directions = _compiled.CreateLayer<Direction>();
		Assert.That( _compiled.ChunkLayout.TryGetChunk( chunkColumn + _chunkOffset, chunkRow + _chunkOffset, out int chunk ), Is.True );
		float[] paddedValues = new float[AtmosphereSimd.PaddedArea];
		Direction[] paddedDirections = new Direction[AtmosphereSimd.PaddedArea];
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
		AtmosphereSimd.Gather( values, chunk, paddedValues );
		AtmosphereSimd.Gather( directions, chunk, paddedDirections );
		values.CopyPadded( chunk, 1, paddedValues );
		directions.CopyPadded( chunk, 1, paddedDirections );
	}

	private void Fill<T>( IGridLayer<T> layer, Func<int, int, T> value ) where T : unmanaged {
		GridLayout layout = _compiled.ChunkLayout;
		foreach( int chunk in layout.EnumerateChunks() ) {
			Span<T> values = layer.GetValues( chunk );
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			for( int i = 0; i < values.Length; i++ ) {
				if( ( presence[i / AtmosphereSimd.ChunkSize] & ( 1UL << ( i % AtmosphereSimd.ChunkSize ) ) ) != 0 ) {
					values[i] = value( i, chunk + 1 );
				}
			}
		}
	}

	private void AssertMatches<T>( IGridLayer<T> layer, int chunkColumn, int chunkRow, T sentinel ) where T : unmanaged {
		GridLayout layout = _compiled.ChunkLayout;
		chunkColumn += _chunkOffset;
		chunkRow += _chunkOffset;
		Assert.That( layout.TryGetChunk( chunkColumn, chunkRow, out int target ), Is.True );
		T[] expected = new T[AtmosphereSimd.PaddedArea + 4];
		T[] copied = new T[expected.Length];
		T[] actual = new T[expected.Length];
		Array.Fill( expected, sentinel );
		Array.Fill( copied, sentinel );
		Array.Fill( actual, sentinel );
		for( int row = 0; row < AtmosphereSimd.PaddedStride; row++ ) {
			for( int column = 0; column < AtmosphereSimd.PaddedStride; column++ ) {
				int worldColumn = ( chunkColumn * AtmosphereSimd.ChunkSize ) + column - 1;
				int worldRow = ( chunkRow * AtmosphereSimd.ChunkSize ) + row - 1;
				expected[( row * AtmosphereSimd.PaddedStride ) + column] = layout.TryLocate( worldColumn, worldRow, out int source, out int local )
					? layer.GetValues( source )[local]
					: default;
			}
		}

		layer.CopyPadded( target, AtmosphereSimd.Halo, copied );
		AtmosphereSimd.Gather( layer, target, actual );

		Assert.That( copied, Is.EqualTo( expected ) );
		Assert.That( actual, Is.EqualTo( expected ) );
	}

}
