using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereLimiter {

	// Writes the per-cell factor that keeps outflow within the available gas.
	void ComputeScale(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float> total,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> vent,
		IGridLayer<float> scale
	);

	// Scales each flow by the factor of the cell it drains from.
	void Limit(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> vent,
		IGridLayer<float> scale
	);

}
