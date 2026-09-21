using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.Temperature;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddSimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IGridDiffusion, GridDiffusion>()
			.AddSingleton<IGridRadiation, GridRadiation>()
			.AddSingleton<IGridTemperature, GridTemperature>();
	}
}
