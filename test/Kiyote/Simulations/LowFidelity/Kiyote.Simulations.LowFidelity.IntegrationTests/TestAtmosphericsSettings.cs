using Kiyote.Simulations.LowFidelity.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.IntegrationTests;

internal sealed class TestAtmosphericsSettings : IAtmosphericsSettings {

	public float Acceleration { get; init; } = 2.0f;

	public float Friction { get; init; } = 0.5f;

	public float Conduction { get; init; } = 0.1f;

	public float HeatCapacity { get; init; } = 20.8f;

	public float CondensationRate { get; init; } = 0.05f;


	public int MaxStepsPerUpdate { get; init; } = 160;

	public float WindScale { get; init; } = 1.0f;

}
