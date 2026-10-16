namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereWind {

	// Converts pressure (holding the total gas on entry) into pressure, and derives wind from the face flows.
	// flowEast and flowSouth are padded spans; every other span is a chunk span (see AtmosphereSimd).
	void Publish(
		ReadOnlySpan<ulong> presence,
		float windScale,
		ReadOnlySpan<float> temperature,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		Span<float> pressure,
		Span<float> windX,
		Span<float> windY
	);

}
