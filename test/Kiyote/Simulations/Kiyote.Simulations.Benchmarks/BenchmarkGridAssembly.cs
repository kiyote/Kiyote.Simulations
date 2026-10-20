using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Benchmarks;

internal static class BenchmarkGridAssembly {

	public static ICompiledGridAssembly<float> Create( int size, int chunkSize, int retainedEmptyChunks ) {
		int firstEmptyColumn = ( ( size + chunkSize - 1 ) / chunkSize ) * chunkSize;
		int width = retainedEmptyChunks == 0 ? size : firstEmptyColumn + ( retainedEmptyChunks * chunkSize );
		DenseGridSource<float> source = new DenseGridSource<float>( width, size );
		for( int row = 0; row < size; row++ ) {
			for( int column = 0; column < size; column++ ) {
				if( !source.TrySetCell( column, row, 0.0f ) ) {
					throw new InvalidOperationException( "Unable to create benchmark cell." );
				}
			}
		}
		for( int i = 0; i < retainedEmptyChunks; i++ ) {
			if( !source.TrySetCell( firstEmptyColumn + ( i * chunkSize ), 0, 0.0f ) ) {
				throw new InvalidOperationException( "Unable to create cell in future empty chunk." );
			}
		}
		IGridAssembly<float> assembly = new GridAssembly<float>();
		AttachResult attached = assembly.TryAttach( source, 0, 0 );
		if( !attached.Succeeded ) {
			throw new InvalidOperationException( "Unable to attach benchmark source." );
		}
		ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, chunkSize );
		bool initialized = false;
		try {
			for( int i = 0; i < retainedEmptyChunks; i++ ) {
				if( !assembly.TryRemoveCell( attached.Placement, firstEmptyColumn + ( i * chunkSize ), 0 ) ) {
					throw new InvalidOperationException( "Unable to empty allocated benchmark chunk." );
				}
			}
			int empty = 0;
			foreach( int chunk in compiled.ChunkLayout.EnumerateChunks() ) {
				if( compiled.ChunkLayout.GetPopulatedCount( chunk ) == 0 ) {
					empty++;
				}
			}
			if( empty != retainedEmptyChunks ) {
				throw new InvalidOperationException( "The benchmark did not retain the required empty chunks." );
			}
			initialized = true;
			return compiled;
		} finally {
			if( !initialized ) {
				compiled.Dispose();
			}
		}
	}

}
