using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridAirflow;
using Microsoft.Extensions.DependencyInjection;
using Kiyote.Simulations.Visualizer.Grids.GridFluid;
using Kiyote.Simulations.Visualizer.Diffusion;
using GridPressureVisualizer = Kiyote.Simulations.Visualizer.Pressure.GridPressureVisualizer;

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
			.AddSingleton<GridPressureVisualizer>();
		/*
			.AddSingleton<GridFluidVisualizer>()
			.AddSingleton<GridAirflowVisualizer>();
		*/

		IServiceProvider services = collection.BuildServiceProvider();

		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );

		GridPressureVisualizer gridPressureVisualizer = services.GetRequiredService<GridPressureVisualizer>();
		gridPressureVisualizer.Execute( outputFolder );

		/*
		GridFluidVisualizer gridFluidVisualizer = services.GetRequiredService<GridFluidVisualizer>();
		gridFluidVisualizer.Execute( outputFolder );

		GridAirflowVisualizer gridAirflowVisualizer = services.GetRequiredService<GridAirflowVisualizer>();
		gridAirflowVisualizer.Execute( outputFolder );
		*/
	}

}
