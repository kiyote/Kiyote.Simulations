using Kiyote.Geometry;

namespace Kiyote.Simulations.LowFidelity.Thermals;

// Borrows the caller's compilation. Dispose before the compilation; commit through its owner.
public interface IThermal : IThermalState, IDisposable {

	// Returns the number of fixed steps taken.
	int Update(
		TimeSpan elapsed
	);

	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
