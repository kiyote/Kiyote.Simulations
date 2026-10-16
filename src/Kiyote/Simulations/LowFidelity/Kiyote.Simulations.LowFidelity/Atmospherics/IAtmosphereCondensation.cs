namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereCondensation {

	// Exchanges one gas and its condensate toward the gas's condensation point.
	// All spans are chunk spans (see AtmosphereSimd). modified is set when any value changed.
	// Returns whether any cell changed phase by more than epsilon.
	bool Condense(
		ReadOnlySpan<ulong> presence,
		float condensation,
		float condensationPoint,
		ReadOnlySpan<float> temperatureNext,
		Span<float> gasNext,
		Span<float> condensate,
		out bool modified
	);

}
