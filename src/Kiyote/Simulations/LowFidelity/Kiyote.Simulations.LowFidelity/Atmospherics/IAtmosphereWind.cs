using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereWind {

	// Converts pressure (holding the total gas on entry) into pressure, and derives wind from the face flows.
	void Publish(
		int slot,
		ReadOnlySpan<ulong> mask,
		float windScale,
		IGridLayer<float> temperature,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> pressure,
		IGridLayer<float> windX,
		IGridLayer<float> windY
	);

}
