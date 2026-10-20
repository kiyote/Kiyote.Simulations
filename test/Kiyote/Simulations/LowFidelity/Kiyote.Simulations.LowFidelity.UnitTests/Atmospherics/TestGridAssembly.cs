using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

internal static class TestGridAssembly {

	public static ICompiledGridAssembly<float> Create(
		int width,
		int height
	) {
		DenseGridSource<float> source = new DenseGridSource<float>( width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				_ = source.TrySetCell( column, row, 0f );
			}
		}
		return Compile( source );
	}

	public static ICompiledGridAssembly<float> Compile(
		DenseGridSource<float> source
	) {
		IGridAssembly<float> assembly = new GridAssembly<float>();
		Assert.That( assembly.TryAttach( source, 0, 0 ).Succeeded, Is.True );
		return new GridCompiler().Compile( assembly, AtmosphereSimd.ChunkSize );
	}

}
