using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Visualizer;

internal sealed class Program {
	public const int Size = 100;

	public static void Main(
		string[] _
	) {
		string outputFolder = Path.Combine( Path.GetTempPath(), "Kiyote.Simulations.Visualizer" );
		if( !Directory.Exists( outputFolder ) ) {
			Directory.CreateDirectory( outputFolder );
		}

		IServiceCollection collection = new ServiceCollection();
		collection
			.AddBuffers()
			.AddNumericBuffers()
			.AddGridsSimulations()
			.AddPngImaging();

		IServiceProvider services = collection.BuildServiceProvider();

		IAnimationWriter animWriter = services.GetRequiredService<IAnimationWriter>();
		IGridDiffusion gridFlow = services.GetRequiredService<IGridDiffusion>();
		INumericBufferFactory bufferFactory = services.GetRequiredService<INumericBufferFactory>();
		INumericBufferOperator bufferOperator = services.GetRequiredService<INumericBufferOperator>();

		INumericBuffer<byte> stretched = bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> buffer = bufferFactory.Create<double>( Size, Size, 0 );
		BufferGrid<double> grid = new BufferGrid<double>( buffer );
		buffer[10, 10] = 10000;
		buffer[90, 50] = 10000;
		buffer[50, 90] = 10000;

		DiffusionStrategy flow = new DiffusionStrategy( 1.0 );
		AlwaysPassableStrategy passability = new AlwaysPassableStrategy();
		BufferSetCellStrategy callback = new BufferSetCellStrategy( buffer );

		IAnimationBuilder builder = animWriter.StartAnimation( Path.Combine( outputFolder, "griddiffusion.apng" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		bufferOperator.ScaleToRange( buffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 100; i++ ) {
			gridFlow.Flow<double, double, DiffusionStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
				grid,
				flow,
				passability,
				callback
			);

			bufferOperator.ScaleToRange( buffer, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();
	}

}
