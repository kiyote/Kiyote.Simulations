using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereAcceleration {

	// Accelerates east/south face flows and vacuum vents from the pressure field.
	void Accelerate(
		int slot,
		ReadOnlySpan<ulong> mask,
		float k,
		float damp,
		bool eastOpen,
		bool southOpen,
		IGridLayer<float> pressure,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> vent,
		IGridLayer<Direction> connectivity,
		IGridLayer<Direction> vacuum,
		IGridLayer<bool> permeable
	);

}
