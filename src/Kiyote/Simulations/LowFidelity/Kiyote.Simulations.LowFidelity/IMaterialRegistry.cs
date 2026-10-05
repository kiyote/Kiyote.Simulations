namespace Kiyote.Simulations.LowFidelity;

public interface IMaterialRegistry {

	int Count { get; }

	IReadOnlyList<MaterialDefinition> Definitions { get; }

	MaterialDefinition GetDefinition(
		MaterialIndex material
	);

	MaterialIndex GetIndex(
		string id
	);

	bool TryGetIndex(
		string id,
		out MaterialIndex material
	);

}
