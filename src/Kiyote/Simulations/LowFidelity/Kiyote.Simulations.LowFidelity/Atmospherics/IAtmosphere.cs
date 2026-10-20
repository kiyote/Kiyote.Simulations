using Kiyote.Geometry;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Borrows the caller's compilation. Dispose before the compilation; commit through its owner.
public interface IAtmosphere : IAtmosphereState, IDisposable {

	// Returns the number of fixed steps run.
	int Update(
		TimeSpan elapsed
	);

	// Required after live occupancy or permeability edits, before Update; use assembly coordinates.
	// Empty areas do nothing. The parameterless overload refreshes everything.
	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
