namespace Kiyote.Simulations.Manager;

public sealed record SimulationManagerOptions(
	TimeSpan FixedTimeStep,
	TimeSpan MaximumScaledDeltaTime
);
