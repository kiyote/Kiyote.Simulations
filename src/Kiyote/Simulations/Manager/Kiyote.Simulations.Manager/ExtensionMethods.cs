using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Manager;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddSimulationManager(
		this IServiceCollection services,
		SimulationManagerOptions options
	) {
		return services
			.AddSingleton( options )
			.AddSingleton<ISimulationManager, SimulationManager>();
	}
}
