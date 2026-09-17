using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Grids;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddGridsSimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<ISealedAnalyzer, SealedAnalyzer>()
			.AddSingleton<IGridDiffusion, GridDiffusion>();
	}
}
