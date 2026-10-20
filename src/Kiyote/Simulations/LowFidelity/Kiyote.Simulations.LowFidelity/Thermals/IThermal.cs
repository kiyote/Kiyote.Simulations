using Kiyote.Geometry;

namespace Kiyote.Simulations.LowFidelity.Thermals;

// Borrows the caller's compilation. Dispose before the compilation; commit through its owner.
public interface IThermal : IThermalState, IDisposable {

	// Returns the number of fixed steps taken.
	int Update(
		TimeSpan elapsed
	);

	// Required after live occupancy or material edits, before Update; use assembly coordinates.
	// Empty areas do nothing. The parameterless overload refreshes everything.
	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
