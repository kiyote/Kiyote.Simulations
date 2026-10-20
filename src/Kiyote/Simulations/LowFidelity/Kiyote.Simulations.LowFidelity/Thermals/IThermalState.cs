using Kiyote.Geometry;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IThermalState {

	IMaterialRegistry Materials { get; }

	// Kelvin per cell.
	IGridLayer<float> Temperature { get; }

	// Direction from the ship towards the sun, or a zero vector for no sun.
	// Treated as an infinitely wide line source, so every lit exterior face sees parallel rays.
	Vector SunDirection { get; set; }

	// Adds
	void AddEnergy(
		int column,
		int row,
		float joules
	);

}
