using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public static class ExtensionMethods {

	public static IServiceCollection AddAtmospherics(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IAtmospherePressure, AtmospherePressure>()
			.AddSingleton<IAtmosphereAcceleration, AtmosphereAcceleration>()
			.AddSingleton<IAtmosphereLimiter, AtmosphereLimiter>()
			.AddSingleton<IAtmosphereTransport, AtmosphereTransport>()
			.AddSingleton<IAtmosphereThermal, AtmosphereThermal>()
			.AddSingleton<IAtmosphereCondensation, AtmosphereCondensation>()
			.AddSingleton<IAtmosphereWind, AtmosphereWind>()
			.AddSingleton<IGridAtmospherics, GridAtmospherics>();
	}
}
