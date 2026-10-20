using Kiyote.Geometry;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

// Runs the atmosphere and the structure's thermal simulation on the same ship and moves heat
// between them: each gas cell exchanges with its own floor and every adjacent wall (an occupied
// cell impermeable to gas), at a rate set by that structure cell's material.
public interface IAtmosphericThermals : IDisposable {

	IAtmosphereState Atmosphere { get; }

	IThermalState Thermal { get; }

	// Steps both children once per shared tick, then performs any exchange due at that tick.
	// Publishes the final coupled state once, including queued inputs on zero-step updates.
	// Uses the smaller child step cap; excess time is dropped. Returns the number of exchanges.
	int Update(
		TimeSpan elapsed
	);

	// Invalidates both children and convection links after live occupancy, permeability or material edits.
	// Use assembly coordinates. Empty areas do nothing; the parameterless overload refreshes everything.
	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
