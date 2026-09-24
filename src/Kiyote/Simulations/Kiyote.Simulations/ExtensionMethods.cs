using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.Pressure;
using Kiyote.Simulations.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddSimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<ISimulationClock, SimulationClock>()
			.AddSingleton<IGridDiffusion, GridDiffusion>()
			.AddSingleton<IGridPressure, GridPressure>()
			.AddSingleton<IGridProjection, GridProjection>();
	}
}
