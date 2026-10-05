using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalSolar {

	// Computes the absorbed solar power (W) of every cell for parallel rays arriving from sunDirection.
	// A vacuum-facing face is lit when it faces the sun and the ray towards the sun leaves the grid
	// without passing through another occupied cell.
	void Illuminate(
		IGridChunkLayout layout,
		Vector sunDirection,
		float flux,
		IGridLayer<float> absorptivity,
		IGridLayer<Direction> vacuum,
		IGridLayer<float> solar
	);

}
