namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereLimiter {

	// Writes the per-cell factor that keeps outflow within the available gas.
	// flowEast and flowSouth are padded spans; total, vent and scale are chunk spans (see AtmosphereSimd).
	void ComputeScale(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> vent,
		Span<float> scale
	);

	// Scales each flow by the factor of the cell it drains from.
	// scale is a padded span; flowEast, flowSouth and vent are chunk spans.
	void Limit(
		ReadOnlySpan<ulong> presence,
		Span<float> flowEast,
		Span<float> flowSouth,
		Span<float> vent,
		ReadOnlySpan<float> scale
	);

}
