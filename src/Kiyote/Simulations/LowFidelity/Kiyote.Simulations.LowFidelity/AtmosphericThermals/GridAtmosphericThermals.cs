using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class GridAtmosphericThermals : IGridAtmosphericThermals {

	private readonly GridAtmospherics _atmospherics;
	private readonly GridThermals _thermals;
	private readonly IMaterialRegistry _materials;
	private readonly IAtmosphericsSettings _atmosphericsSettings;
	private readonly IAtmosphericThermalsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IAtmosphericThermalsConvection _convection;

	public GridAtmosphericThermals(
		GridAtmospherics atmospherics,
		GridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		IAtmosphericThermalsSettings settings,
		ISimulationClock clock,
		IAtmosphericThermalsConvection convection
	) {
		_atmospherics = atmospherics;
		_thermals = thermals;
		_materials = materials;
		_atmosphericsSettings = atmosphericsSettings;
		_settings = settings;
		_clock = clock;
		_convection = convection;
	}

	IAtmosphericThermals IGridAtmosphericThermals.Create<TCell, TAtmosphereStrategy, TThermalStrategy>(
		ICompiledGridAssembly<TCell> compiled,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy
	) {
		return new AtmosphericThermals<TCell, TAtmosphereStrategy, TThermalStrategy>(
			compiled,
			atmosphereStrategy,
			thermalStrategy,
			_atmospherics,
			_thermals,
			_materials,
			_atmosphericsSettings,
			_settings,
			_clock,
			_convection
		);
	}

}
