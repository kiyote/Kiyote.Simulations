namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IGasRegistry {

	int Count { get; }

	IReadOnlyList<GasDefinition> Definitions { get; }

	GasDefinition GetDefinition(
		GasIndex gas
	);

	GasIndex GetIndex(
		string id
	);

	bool TryGetIndex(
		string id,
		out GasIndex gas
	);

}
