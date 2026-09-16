using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Grids;

public static class ExtensionMethods {

	public static IServiceCollection AddGridsSimulations(
		this IServiceCollection services
	) {
		return services.AddSingleton<ISealedAnalyzer, SealedAnalyzer>();
	}
}
