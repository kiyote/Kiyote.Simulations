using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class GridThermals : IGridThermals {

	private readonly IMaterialRegistry _materials;
	private readonly IThermalsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IGridCompiler _compiler;
	private readonly IThermalConduction _conduction;
	private readonly IThermalRadiation _radiation;
	private readonly IThermalSolar _solar;

	public GridThermals(
		IMaterialRegistry materials,
		IThermalsSettings settings,
		ISimulationClock clock,
		IGridCompiler compiler,
		IThermalConduction conduction,
		IThermalRadiation radiation,
		IThermalSolar solar
	) {
		_materials = materials;
		_settings = settings;
		_clock = clock;
		_compiler = compiler;
		_conduction = conduction;
		_radiation = radiation;
		_solar = solar;
	}

	IThermal IGridThermals.Create<TCell, TStrategy>(
		IGridAssembly<TCell> ship,
		TStrategy strategy
	) {
		return new Thermal<TCell, TStrategy>(
			ship,
			strategy,
			_materials,
			_settings,
			_clock,
			_compiler,
			_conduction,
			_radiation,
			_solar
		);
	}

}
