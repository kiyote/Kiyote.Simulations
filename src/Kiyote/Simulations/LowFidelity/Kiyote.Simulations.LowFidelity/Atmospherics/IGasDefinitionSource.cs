namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IGasDefinitionSource {

	IEnumerable<GasDefinition> GetDefinitions();

}
