namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public sealed class GasRegistryBuilder : IGasRegistryBuilder {

	private readonly IEnumerable<IGasDefinitionSource> _sources;

	public GasRegistryBuilder(
		IEnumerable<IGasDefinitionSource> sources
	) {
		_sources = sources;
	}

	IGasRegistry IGasRegistryBuilder.Build() {
		GasDefinition[] definitions = [..
			_sources
				.SelectMany( s => s.GetDefinitions() )
				.OrderBy( d => d.Id, StringComparer.Ordinal )
		];
		return new GasRegistry( definitions );
	}

}
