using Kiyote.Simulations.LowFidelity.Thermals;
using Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Thermals;

internal readonly struct ThermalCellStrategy : IThermalCellStrategy<AtmosphereCell> {

	private readonly MaterialIndex _material;
	private readonly float _initialTemperature;

	public ThermalCellStrategy(
		IMaterialRegistry materials,
		float initialTemperature
	) {
		_material = materials.GetIndex( "aluminium" );
		_initialTemperature = initialTemperature;
	}

	public MaterialIndex GetMaterial( in AtmosphereCell cell ) => _material;
	public float GetTemperature( in AtmosphereCell cell ) => _initialTemperature;
	public void SetTemperature( ref AtmosphereCell cell, float temperature ) { }
}

// One cell is treated as a 1m x 1m face; at 1 AU the sun delivers ~1361 W/m^2.
internal sealed class ThermalsSettings : IThermalsSettings {
	public const float StefanBoltzmannConstant = 5.67e-8f;

	// Settable so a visualization can switch radiation off; the simulation reads it every step.
	public float StefanBoltzmann { get; set; } = StefanBoltzmannConstant;
	public float SpaceTemperature { get; init; } = 2.7f;
	public float SolarFlux { get; init; } = 1361.0f;
	public float FixedTimeStep { get; init; } = 10.0f;
	public int MaxStepsPerAdvance { get; init; } = 2000;
}

// Each cell is a 1m x 1m aluminium plate, 1cm thick (27kg):
//   conductance = k * A / L = 205 W/mK * (1m * 0.01m) / 1m = 2.05 W/K
//   capacity    = 27kg * 897 J/kgK ~= 24200 J/K
//   bare metal is a poor emitter and absorber (e ~0.1, a ~0.15).
internal sealed class MaterialDefinitionSource : IMaterialDefinitionSource {
	public IEnumerable<MaterialDefinition> GetDefinitions() {
		yield return new MaterialDefinition( "aluminium", "Aluminium", Conductivity: 2.05f, HeatCapacity: 24200.0f, Emissivity: 0.1f, Absorptivity: 0.15f );
	}
}
