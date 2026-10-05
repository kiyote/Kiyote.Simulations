using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Thermals;

public static class ExtensionMethods {

	public static IServiceCollection AddThermals(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IThermalConduction, ThermalConduction>()
			.AddSingleton<IThermalRadiation, ThermalRadiation>()
			.AddSingleton<IThermalSolar, ThermalSolar>()
			.AddSingleton<IGridThermals, GridThermals>();
	}
}
