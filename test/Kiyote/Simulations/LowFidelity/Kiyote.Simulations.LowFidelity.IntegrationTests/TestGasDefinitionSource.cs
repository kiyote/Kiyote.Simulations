using Kiyote.Simulations.LowFidelity.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.IntegrationTests;

internal sealed class TestGasDefinitionSource : IGasDefinitionSource {
	public IEnumerable<GasDefinition> GetDefinitions() {
		yield return new GasDefinition( "O2", "Oxygen", 90.2f, 54.4f );
		yield return new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f );
	}
}
