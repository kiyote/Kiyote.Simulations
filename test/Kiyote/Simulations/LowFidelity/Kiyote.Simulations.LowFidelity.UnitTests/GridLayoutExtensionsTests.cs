using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.UnitTests;

[TestFixture]
public sealed class GridLayoutExtensionsTests {

	[TestCase( 32 )]
	[TestCase( 64 )]
	[TestCase( 128 )]
	public void TryGetPopulatedBounds_SparseNegativePlacement_ReturnsOccupiedExtent(
		int chunkSize
	) {
		DenseGridSource<float> source = new DenseGridSource<float>( 130, 4 );
		_ = source.TrySetCell( 1, 2, 0f );
		_ = source.TrySetCell( 129, 3, 0f );
		IGridAssembly<float> assembly = new GridAssembly<float>();
		Assert.That( assembly.TryAttach( source, -33, -34 ).Succeeded, Is.True );
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, chunkSize );

		bool found = compiled.ChunkLayout.TryGetPopulatedBounds( out int column, out int row, out int width, out int height );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( found, Is.True );
			Assert.That( (column, row, width, height), Is.EqualTo( (-32, -32, 129, 2) ) );
		}
	}

	[Test]
	public void TryGetPopulatedBounds_EmptyAssembly_ReturnsFalseAndZeroBounds() {
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( new GridAssembly<float>(), 32 );

		bool found = compiled.ChunkLayout.TryGetPopulatedBounds( out int column, out int row, out int width, out int height );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( found, Is.False );
			Assert.That( (column, row, width, height), Is.EqualTo( (0, 0, 0, 0) ) );
		}
	}

	[Test]
	public void TryGetPopulatedBounds_LastCellRemoved_IgnoresAllocatedEmptyChunk() {
		DenseGridSource<float> source = new DenseGridSource<float>( 1, 1 );
		_ = source.TrySetCell( 0, 0, 0f );
		IGridAssembly<float> assembly = new GridAssembly<float>();
		AttachResult attached = assembly.TryAttach( source, 0, 0 );
		Assert.That( attached.Succeeded, Is.True );
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, 32 );
		Assert.That( assembly.TryRemoveCell( attached.Placement, 0, 0 ), Is.True );

		bool found = compiled.ChunkLayout.TryGetPopulatedBounds( out int column, out int row, out int width, out int height );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( compiled.ChunkLayout.ChunkCount, Is.EqualTo( 1 ) );
			Assert.That( found, Is.False );
			Assert.That( (column, row, width, height), Is.EqualTo( (0, 0, 0, 0) ) );
		}
	}

}
