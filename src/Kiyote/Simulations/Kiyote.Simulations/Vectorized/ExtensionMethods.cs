using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Vectorized.Diffusion;
using Kiyote.Simulations.Vectorized.Pressure;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.Vectorized;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	// Registers the vectorized simulations. Callers must also register the settings
	// each simulation consumes (e.g. IGridDiffusionSettings, IGridPressureSettings) and
	// an ISimulationClock.
	public static IServiceCollection AddVectorizedSimulations(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IFieldCompiler, FieldCompiler>()
			.AddSingleton<IGridDiffusion, GridDiffusion>()
			.AddSingleton<IGridPressure, GridPressure>();
	}
}
