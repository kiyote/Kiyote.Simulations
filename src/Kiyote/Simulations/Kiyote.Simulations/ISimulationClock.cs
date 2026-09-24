namespace Kiyote.Simulations;

public interface ISimulationClock {

	// The fixed, stability-bounded time interval that low-level grid simulations
	// (diffusion, pressure, etc.) advance by on each call to their Update method.
	// Higher-level simulations requesting a larger duration are expected to invoke
	// low-level Update calls repeatedly until that duration has been covered.
	float FixedTimeStep { get; }

}
