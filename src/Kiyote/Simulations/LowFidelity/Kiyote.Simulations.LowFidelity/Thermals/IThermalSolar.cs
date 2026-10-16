using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalSolar {

	// Computes the absorbed solar power (W) of every cell for parallel rays arriving from sunDirection.
	// A vacuum-facing face is lit when it faces the sun and the ray towards the sun leaves the grid
	// without passing through another occupied cell. Every layer shares layout.
	void Illuminate(
		ChunkedGridLayout layout,
		Vector sunDirection,
		float flux,
		ChunkedGridLayer<float> absorptivity,
		ChunkedGridLayer<Direction> vacuum,
		ChunkedGridLayer<float> solar
	);

}
