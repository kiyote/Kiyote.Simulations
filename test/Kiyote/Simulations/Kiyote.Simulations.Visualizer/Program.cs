using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridDiffusion;
using Kiyote.Simulations.Visualizer.GridFluid;
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
			.AddGridsSimulations()
			.AddPngImaging()
			.AddSingleton<GridDiffusionVisualizer>()
			.AddSingleton<GridFluidVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );

		GridFluidVisualizer gridFluidVisualizer = services.GetRequiredService<GridFluidVisualizer>();
		gridFluidVisualizer.Execute( outputFolder );
	}

}
