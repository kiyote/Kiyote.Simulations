using Kiyote.Geometry;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

// Runs the atmosphere and the structure's thermal simulation on the same ship and moves heat
// between them: each gas cell exchanges with its own floor and every adjacent wall (an occupied
// cell impermeable to gas), at a rate set by that structure cell's material.
public interface IAtmosphericThermals : IDisposable {

	IAtmosphere Atmosphere { get; }

	IThermal Thermal { get; }

	// Updates both simulations, then runs any heat exchanges that have come due.
	// Returns the number of heat exchanges run.
	int Update(
		TimeSpan elapsed
	);

	// Writes both simulations back to the cells.
	void Commit();

	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
