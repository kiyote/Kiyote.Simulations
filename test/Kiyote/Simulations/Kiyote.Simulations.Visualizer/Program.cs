using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Visualizer.Topology.Advection;
using Kiyote.Simulations.Visualizer.Topology.Airflow;
using Kiyote.Simulations.Visualizer.Topology.Diffusion;
using Kiyote.Simulations.Visualizer.Topology.Pressure;
using Kiyote.Simulations.Visualizer.Topology.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Visualizer;

internal sealed class Program {
	public static void Main(
		string[] _
	) {
		string outputFolder = Path.Combine( Path.GetTempPath(), "Kiyote.Simulations.Visualizer" );
		Directory.CreateDirectory( outputFolder );

		IServiceCollection collection = new ServiceCollection();
		collection
			.AddRaggedBuffers()
			.AddRaggedNumericBuffers()
			.AddSimulations()
			.AddGifImaging()
			.AddSingleton<GridDiffusionVisualizer>()
			.AddSingleton<GridPressureVisualizer>()
			.AddSingleton<GridProjectionVisualizer>()
			.AddSingleton<GridAdvectionVisualizer>()
			.AddSingleton<GridAirflowVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		Console.Write( "Topology Diffusion..." );
		services.GetRequiredService<GridDiffusionVisualizer>().Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Topology Pressure..." );
		services.GetRequiredService<GridPressureVisualizer>().Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Topology Projection..." );
		services.GetRequiredService<GridProjectionVisualizer>().Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Topology Advection..." );
		services.GetRequiredService<GridAdvectionVisualizer>().Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Topology Airflow..." );
		services.GetRequiredService<GridAirflowVisualizer>().Execute( outputFolder );
		Console.WriteLine( "...Done" );
	}

}
