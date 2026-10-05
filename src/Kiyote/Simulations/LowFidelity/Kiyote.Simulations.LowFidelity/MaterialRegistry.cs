namespace Kiyote.Simulations.LowFidelity;

internal sealed class MaterialRegistry : IMaterialRegistry {

	private readonly MaterialDefinition[] _definitions;
	private readonly Dictionary<string, MaterialIndex> _lookup;

	public MaterialRegistry(
		MaterialDefinition[] definitions
	) {
		_definitions = definitions;
		_lookup = new Dictionary<string, MaterialIndex>( StringComparer.Ordinal );
		for( int i = 0; i < definitions.Length; i++ ) {
			if( !_lookup.TryAdd( definitions[i].Id, new MaterialIndex( i ) ) ) {
				throw new InvalidOperationException( $"Material '{definitions[i].Id}' is defined more than once." );
			}
		}
	}

	int IMaterialRegistry.Count => _definitions.Length;

	IReadOnlyList<MaterialDefinition> IMaterialRegistry.Definitions => _definitions;

	MaterialDefinition IMaterialRegistry.GetDefinition(
		MaterialIndex material
	) {
		return _definitions[material.Value];
	}

	MaterialIndex IMaterialRegistry.GetIndex(
		string id
	) {
		if( _lookup.TryGetValue( id, out MaterialIndex material ) ) {
			return material;
		}
		throw new KeyNotFoundException( $"Material '{id}' is not registered." );
	}

	bool IMaterialRegistry.TryGetIndex(
		string id,
		out MaterialIndex material
	) {
		return _lookup.TryGetValue( id, out material );
	}

}
