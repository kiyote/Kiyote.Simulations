using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations;

[ExcludeFromCodeCoverage]
public static class ExtensionMethods {

	public static IServiceCollection AddSimulations(
		this IServiceCollection services
	) {
		return services;
	}
}
