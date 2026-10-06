namespace Kiyote.Simulations.LowFidelity;

public interface ISimulationClock {

	// The fixed, stability-bounded time interval, in seconds, that each simulation
	// advances by per internal step. Larger Update durations run repeated steps.
	float FixedTimeStep { get; }

}
