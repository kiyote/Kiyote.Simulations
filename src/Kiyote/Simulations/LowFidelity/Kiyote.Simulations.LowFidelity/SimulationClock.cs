namespace Kiyote.Simulations.LowFidelity;

public sealed class SimulationClock : ISimulationClock {

	float ISimulationClock.FixedTimeStep => 0.1f;

}
