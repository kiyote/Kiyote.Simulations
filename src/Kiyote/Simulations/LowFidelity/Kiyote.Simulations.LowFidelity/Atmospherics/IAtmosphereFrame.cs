namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// A read-only copy of the atmosphere as of the end of an Update.
// Obtained from IAtmosphere.AcquireFrame and owned exclusively by the reader until ReleaseFrame.
// Spans are indexed with IndexOf; cells outside the grid return -1.
public interface IAtmosphereFrame {

	IGasRegistry Gases { get; }

	// Total fixed steps run when the frame was captured.
	long StepCount { get; }

	// Total gas lost to space during the Update that produced the frame.
	float Vented { get; }

	// kPa
	ReadOnlySpan<float> Pressure { get; }

	// K
	ReadOnlySpan<float> Temperature { get; }

	// kPa of dynamic pressure, +x = east.
	ReadOnlySpan<float> WindX { get; }

	// kPa of dynamic pressure, +y = south.
	ReadOnlySpan<float> WindY { get; }

	ReadOnlySpan<float> GetGas(
		GasIndex gas
	);

	// Only available for gases with a condensation point.
	ReadOnlySpan<float> GetCondensate(
		GasIndex gas
	);

	int IndexOf(
		int column,
		int row
	);

}
