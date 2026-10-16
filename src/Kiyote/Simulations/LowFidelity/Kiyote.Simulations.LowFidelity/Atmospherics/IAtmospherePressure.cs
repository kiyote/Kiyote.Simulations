namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// All spans are chunk spans (see AtmosphereSimd).
internal interface IAtmospherePressure {

	// Adds one gas's amounts into total for every present cell.
	void Accumulate(
		ReadOnlySpan<ulong> presence,
		ReadOnlySpan<float> gas,
		Span<float> total
	);

	// Derives pressure = total * temperature / ReferenceTemperature for every present cell.
	void Compute(
		ReadOnlySpan<ulong> presence,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> temperature,
		Span<float> pressure
	);

}
