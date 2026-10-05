namespace Kiyote.Simulations.LowFidelity;

public interface IGasDefinitionSource {

	IEnumerable<GasDefinition> GetDefinitions();

}
