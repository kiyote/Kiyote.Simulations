using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class GridAtmosphericThermals : IGridAtmosphericThermals {

	private readonly IGridAtmospherics _atmospherics;
	private readonly IGridThermals _thermals;
	private readonly IMaterialRegistry _materials;
	private readonly IAtmosphericsSettings _atmosphericsSettings;
	private readonly IAtmosphericThermalsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IGridCompiler _compiler;
	private readonly IAtmosphericThermalsConvection _convection;

	public GridAtmosphericThermals(
		IGridAtmospherics atmospherics,
		IGridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		IAtmosphericThermalsSettings settings,
		ISimulationClock clock,
		IGridCompiler compiler,
		IAtmosphericThermalsConvection convection
	) {
		_atmospherics = atmospherics;
		_thermals = thermals;
		_materials = materials;
		_atmosphericsSettings = atmosphericsSettings;
		_settings = settings;
		_clock = clock;
		_compiler = compiler;
		_convection = convection;
	}

	IAtmosphericThermals IGridAtmosphericThermals.Create<TCell, TAtmosphereStrategy, TThermalStrategy>(
		IGridAssembly<TCell> ship,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy
	) {
		return new AtmosphericThermals<TCell, TAtmosphereStrategy, TThermalStrategy>(
			ship,
			atmosphereStrategy,
			thermalStrategy,
			_atmospherics,
			_thermals,
			_materials,
			_atmosphericsSettings,
			_settings,
			_clock,
			_compiler,
			_convection
		);
	}

}
