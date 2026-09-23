using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Diffusion;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddSimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IGridDiffusion, GridDiffusion>();
	}
}
