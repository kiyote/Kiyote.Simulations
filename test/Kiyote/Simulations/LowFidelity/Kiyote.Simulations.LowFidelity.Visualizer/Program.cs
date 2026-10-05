using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Visualizer;

internal sealed class Program {
	public static void Main(
		string[] _
	) {
		string outputFolder = Path.Combine( Path.GetTempPath(), "Kiyote.Simulations.LowFidelity.Visualizer" );
		if( !Directory.Exists( outputFolder ) ) {
			Directory.CreateDirectory( outputFolder );
		}

		IServiceCollection collection = new ServiceCollection();
		collection
			.AddRaggedBuffers()
			.AddRaggedNumericBuffers()
			.AddGifImaging()
			.AddSingleton( ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasDefinitionSource()] ) ).Build() )
			.AddSingleton<IAtmosphericsSettings, AtmosphericsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.AddSingleton<AtmosphereVisualizer>();

		IServiceProvider services = collection.BuildServiceProvider();

		Console.Write( "Atmosphere..." );
		AtmosphereVisualizer atmosphereVisualizer = services.GetRequiredService<AtmosphereVisualizer>();
		atmosphereVisualizer.Execute( outputFolder );
		Console.WriteLine( "...Done" );
	}

}
