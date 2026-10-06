using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddLowFidelitySimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<ISimulationClock, SimulationClock>()
			.AddAtmospherics()
			.AddThermals()
			.AddAtmosphericThermals();
	}
}
