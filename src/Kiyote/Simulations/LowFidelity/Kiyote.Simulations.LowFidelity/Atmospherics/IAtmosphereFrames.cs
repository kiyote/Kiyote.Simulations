namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// One consumer may lease the latest complete frame while the simulation publishes new frames.
// Dispose the lease before acquiring again, including after topology changes. Layer views and
// spans are read-only and valid only for the lease's lifetime. Acquisition does not wait for a
// new update, copy layers or allocate. Publication never waits for the consumer.
public interface IAtmosphereFrames {

	IGasRegistry Gases { get; }

	// Latest published diagnostics. Acquire a frame to read these together with its layers.
	long StepCount { get; }

	float Vented { get; }

	// Throws InvalidOperationException if a lease is outstanding or no frame has been published.
	AtmosphereFrameLease AcquireLatest();

}
