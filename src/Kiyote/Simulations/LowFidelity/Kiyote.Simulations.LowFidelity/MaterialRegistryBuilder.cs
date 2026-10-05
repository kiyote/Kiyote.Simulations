namespace Kiyote.Simulations.LowFidelity;

public sealed class MaterialRegistryBuilder : IMaterialRegistryBuilder {

	private readonly IEnumerable<IMaterialDefinitionSource> _sources;

	public MaterialRegistryBuilder(
		IEnumerable<IMaterialDefinitionSource> sources
	) {
		_sources = sources;
	}

	IMaterialRegistry IMaterialRegistryBuilder.Build() {
		MaterialDefinition[] definitions = [..
			_sources
				.SelectMany( s => s.GetDefinitions() )
				.OrderBy( d => d.Id, StringComparer.Ordinal )
		];
		return new MaterialRegistry( definitions );
	}

}
