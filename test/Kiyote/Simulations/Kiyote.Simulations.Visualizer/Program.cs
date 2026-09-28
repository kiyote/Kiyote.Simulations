using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Visualizer.Advection;
using Kiyote.Simulations.Visualizer.Airflow;
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
			.AddSingleton<GridProjectionVisualizer>()
			.AddSingleton<GridAdvectionVisualizer>()
			.AddSingleton<GridAirflowVisualizer>()
			.AddSingleton<Vectorized.Diffusion.GridDiffusionVisualizer>()
			.AddSingleton<Vectorized.Pressure.GridPressureVisualizer>()
			.AddSingleton<Vectorized.Projection.GridProjectionVisualizer>()
			.AddSingleton<Vectorized.Advection.GridAdvectionVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		Console.Write( "Diffusion..." );
		GridDiffusionVisualizer gridDiffusionVisualizer = services.GetRequiredService<GridDiffusionVisualizer>();
		gridDiffusionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Vectorized Diffusion..." );
		Vectorized.Diffusion.GridDiffusionVisualizer vectorizedDiffusionVisualizer = services.GetRequiredService<Vectorized.Diffusion.GridDiffusionVisualizer>();
		vectorizedDiffusionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Pressure..." );
		GridPressureVisualizer gridPressureVisualizer = services.GetRequiredService<GridPressureVisualizer>();
		gridPressureVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Vectorized Pressure..." );
		Vectorized.Pressure.GridPressureVisualizer vectorizedPressureVisualizer = services.GetRequiredService<Vectorized.Pressure.GridPressureVisualizer>();
		vectorizedPressureVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Projection..." );
		GridProjectionVisualizer projectionVisualizer = services.GetRequiredService<GridProjectionVisualizer>();
		projectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Vectorized Projection..." );
		Vectorized.Projection.GridProjectionVisualizer vectorizedProjectionVisualizer = services.GetRequiredService<Vectorized.Projection.GridProjectionVisualizer>();
		vectorizedProjectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Advection..." );
		GridAdvectionVisualizer gridAdvectionVisualizer = services.GetRequiredService<GridAdvectionVisualizer>();
		gridAdvectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		Console.Write( "Vectorized Advection..." );
		Vectorized.Advection.GridAdvectionVisualizer vectorizedAdvectionVisualizer = services.GetRequiredService<Vectorized.Advection.GridAdvectionVisualizer>();
		vectorizedAdvectionVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );

		/*
		Console.Write( "Grid Airflow..." );
		GridAirflowVisualizer gridAirflowVisualizer = services.GetRequiredService<GridAirflowVisualizer>();
		gridAirflowVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );
		*/
	}

}
