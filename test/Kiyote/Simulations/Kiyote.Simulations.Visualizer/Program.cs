using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Visualizer;

internal sealed class Program {
	public const int Size = 30;

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
		IGridFlow gridFlow = services.GetRequiredService<IGridFlow>();
		INumericBufferFactory bufferFactory = services.GetRequiredService<INumericBufferFactory>();
		INumericBufferOperator bufferOperator = services.GetRequiredService<INumericBufferOperator>();

		INumericBuffer<byte> stretched = bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<byte> buffer = bufferFactory.Create<byte>( Size, Size, 0 );
		BufferGrid<byte> grid = new BufferGrid<byte>( buffer );
		buffer[10, 10] = 255;
		
		FlowStrategy flow = new FlowStrategy( 0.1 );
		AlwaysPassableStrategy passability = new AlwaysPassableStrategy();
		BufferSetCellStrategy callback = new BufferSetCellStrategy( buffer );

		IAnimationBuilder builder = animWriter.StartAnimation( Path.Combine( outputFolder, "gridflow.apng" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		bufferOperator.ScaleToRange( buffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 100; i++ ) {
			gridFlow.Flow<byte, int, FlowStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
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
