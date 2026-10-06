using Kiyote.Simulations.LowFidelity.AtmosphericThermals;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Combined;

internal sealed class AtmosphericThermalsSettings : IAtmosphericThermalsSettings {
	public float ExchangeInterval { get; init; } = 5.0f;
}
