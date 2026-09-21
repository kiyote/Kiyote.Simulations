using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;
using Kiyote.Simulations.Visualizer.GridAirflow;
using Microsoft.Extensions.DependencyInjection;
using Kiyote.Simulations.Visualizer.Grids.GridPressure;
using Kiyote.Simulations.Visualizer.Grids.GridFluid;
using Kiyote.Simulations.Visualizer.Grids.GridDiffusion;
using Kiyote.Simulations.Visualizer.Temperature;

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
			.AddSingleton<GridRadiationVisualizer>()
			.AddSingleton<GridTemperatureVisualizer>();
		/*
			.AddSingleton<GridDiffusionVisualizer>()
			.AddSingleton<GridFluidVisualizer>()
			.AddSingleton<GridPressureVisualizer>()
			.AddSingleton<GridAirflowVisualizer>();
		*/

		IServiceProvider services = collection.BuildServiceProvider();

		GridRadiationVisualizer gridRadiationVisualizer = services.GetRequiredService<GridRadiationVisualizer>();
		gridRadiationVisualizer.Execute( outputFolder );

		GridTemperatureVisualizer gridTemperatureVisualizer = services.GetRequiredService<GridTemperatureVisualizer>();
		gridTemperatureVisualizer.Execute( outputFolder );

		/*
		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );

		GridFluidVisualizer gridFluidVisualizer = services.GetRequiredService<GridFluidVisualizer>();
		gridFluidVisualizer.Execute( outputFolder );

		GridPressureVisualizer gridPressureVisualizer = services.GetRequiredService<GridPressureVisualizer>();
		gridPressureVisualizer.Execute( outputFolder );

		GridAirflowVisualizer gridAirflowVisualizer = services.GetRequiredService<GridAirflowVisualizer>();
		gridAirflowVisualizer.Execute( outputFolder );
		*/
	}

}
