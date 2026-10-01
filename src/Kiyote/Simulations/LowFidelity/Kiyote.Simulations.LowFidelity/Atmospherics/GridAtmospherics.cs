using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public sealed class GridAtmospherics : IGridAtmospherics {

	private readonly IGasRegistry _gases;
	private readonly IAtmosphericsSettings _settings;
	private readonly IGridCompiler _compiler;
	private readonly IConnectivityBuilder _connectivityBuilder;

	public GridAtmospherics(
		IGasRegistry gases,
		IAtmosphericsSettings settings,
		IGridCompiler compiler,
		IConnectivityBuilder connectivityBuilder
	) {
		_gases = gases;
		_settings = settings;
		_compiler = compiler;
		_connectivityBuilder = connectivityBuilder;
	}

	IAtmosphere IGridAtmospherics.Create<TCell, TStrategy>(
		IGridAssembly<TCell> ship,
		TStrategy strategy
	) {
		return new Atmosphere<TCell, TStrategy>( ship, strategy, _gases, _settings, _compiler, _connectivityBuilder );
	}

}
