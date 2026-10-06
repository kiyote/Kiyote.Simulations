using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

public static class ExtensionMethods {

	public static IServiceCollection AddAtmosphericThermals(
		this IServiceCollection services
	) {
		return services
			.AddSingleton<IAtmosphericThermalsConvection, AtmosphericThermalsConvection>()
			.AddSingleton<IGridAtmosphericThermals, GridAtmosphericThermals>();
	}
}
