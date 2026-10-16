namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereTransport {

	// Fills outgoing (the gas leaving each cell this step) and zeroes newTotal for the present cells.
	// flowEast and flowSouth are padded spans; vent, outgoing and newTotal are chunk spans (see AtmosphereSimd).
	void Prepare(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> vent,
		Span<float> outgoing,
		Span<float> newTotal
	);

	// Moves one gas along the limited flows into gasNext and adds the result to newTotal.
	// gas, total, flowEast and flowSouth are padded spans; outgoing, gasNext and newTotal are chunk spans.
	void Transport(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> gas,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> outgoing,
		Span<float> gasNext,
		Span<float> newTotal
	);

}
