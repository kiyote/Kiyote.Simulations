using Kiyote.Geometry;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IThermal : IDisposable {

	IMaterialRegistry Materials { get; }

	// Kelvin per cell.
	IGridLayer<float> Temperature { get; }

	// Direction from the ship towards the sun, or a zero vector for no sun.
	// Treated as an infinitely wide line source, so every lit exterior face sees parallel rays.
	Vector SunDirection { get; set; }

	// Returns the number of fixed steps taken.
	int Update(
		TimeSpan elapsed
	);

	// Adds
	void AddEnergy(
		int column,
		int row,
		float joules
	);

	// Writes temperatures back to the cells.
	void Commit();

	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
