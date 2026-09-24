using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Visualizer.Diffusion;
using Kiyote.Simulations.Visualizer.Pressure;
using Kiyote.Simulations.Visualizer.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Visualizer;

internal sealed class Program {
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
			.AddSimulations()
			.AddGifImaging()
			.AddSingleton<GridDiffusionVisualizer>()
			.AddSingleton<GridPressureVisualizer>()
			.AddSingleton<GridProjectionVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );

		GridPressureVisualizer gridPressureVisualizer = services.GetRequiredService<GridPressureVisualizer>();
		gridPressureVisualizer.Execute( outputFolder );

		GridProjectionVisualizer gridProjectionVisualizer = services.GetRequiredService<GridProjectionVisualizer>();
		gridProjectionVisualizer.Execute( outputFolder );

	}

}
