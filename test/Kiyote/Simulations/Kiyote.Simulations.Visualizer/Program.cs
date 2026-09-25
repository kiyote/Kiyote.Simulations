using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Visualizer.Advection;
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
			.AddRaggedBuffers()
			.AddRaggedNumericBuffers()
			.AddSimulations()
			.AddGifImaging()
			.AddSingleton<GridDiffusionVisualizer>()
			.AddSingleton<GridPressureVisualizer>()
			.AddSingleton<OpenGridProjectionVisualizer>()
			.AddSingleton<BoundaryGridProjectionVisualizer>()
			.AddSingleton<GridAdvectionVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		Console.Write( "Diffusion..." );
		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Pressure..." );
		GridPressureVisualizer gridPressureVisualizer = services.GetRequiredService<GridPressureVisualizer>();
		gridPressureVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );
		
		Console.Write( "Open Grid Projection..." );
		OpenGridProjectionVisualizer openProjectionVisualizer = services.GetRequiredService<OpenGridProjectionVisualizer>();
		openProjectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Boundary Grid Projection..." );
		BoundaryGridProjectionVisualizer boundaryProjectionVisualizer = services.GetRequiredService<BoundaryGridProjectionVisualizer>();
		boundaryProjectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Grid Advection..." );
		GridAdvectionVisualizer gridAdvectionVisualizer = services.GetRequiredService<GridAdvectionVisualizer>();
		gridAdvectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );
	}

}
