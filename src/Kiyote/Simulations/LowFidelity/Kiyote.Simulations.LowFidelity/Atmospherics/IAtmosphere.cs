using Kiyote.Geometry;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Borrows the caller's compilation. Dispose before the compilation; commit through its owner.
public interface IAtmosphere : IAtmosphereState, IDisposable {

	// Returns the number of fixed steps run.
	int Update(
		TimeSpan elapsed
	);

	void InvalidateTopology(
		Rect area
	);

	void InvalidateTopology();

}
