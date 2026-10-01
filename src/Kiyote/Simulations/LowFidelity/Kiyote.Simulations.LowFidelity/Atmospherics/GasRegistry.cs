namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class GasRegistry : IGasRegistry {

	private readonly GasDefinition[] _definitions;
	private readonly Dictionary<string, GasIndex> _lookup;

	public GasRegistry(
		GasDefinition[] definitions
	) {
		_definitions = definitions;
		_lookup = new Dictionary<string, GasIndex>( StringComparer.Ordinal );
		for( int i = 0; i < definitions.Length; i++ ) {
			if( !_lookup.TryAdd( definitions[i].Id, new GasIndex( i ) ) ) {
				throw new InvalidOperationException( $"Gas '{definitions[i].Id}' is defined more than once." );
			}
		}
	}

	int IGasRegistry.Count => _definitions.Length;

	IReadOnlyList<GasDefinition> IGasRegistry.Definitions => _definitions;

	GasDefinition IGasRegistry.GetDefinition(
		GasIndex gas
	) {
		return _definitions[gas.Value];
	}

	GasIndex IGasRegistry.GetIndex(
		string id
	) {
		if( _lookup.TryGetValue( id, out GasIndex gas ) ) {
			return gas;
		}
		throw new KeyNotFoundException( $"Gas '{id}' is not registered." );
	}

	bool IGasRegistry.TryGetIndex(
		string id,
		out GasIndex gas
	) {
		return _lookup.TryGetValue( id, out gas );
	}

}
