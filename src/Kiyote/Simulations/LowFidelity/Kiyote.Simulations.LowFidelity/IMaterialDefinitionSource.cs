namespace Kiyote.Simulations.LowFidelity;

public interface IMaterialDefinitionSource {

	IEnumerable<MaterialDefinition> GetDefinitions();

}
