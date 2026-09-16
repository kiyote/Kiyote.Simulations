using Kiyote.Simulations.Grids;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations;

public static class ExtensionMethods {

	public static IServiceCollection AddSimulations(
		this IServiceCollection services
	) {
		return services.AddGridsSimulations();
	}
}
