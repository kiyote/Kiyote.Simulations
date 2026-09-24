using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Pressure;

namespace Kiyote.Simulations.Projection;

// Extends IPressureStrategy with the pieces GridProjection needs to seed and read back
// the pressure-correction field it relaxes via IGridPressure: writing a computed
// divergence value into a TPressure before the relaxation loop runs, and reading the
// resulting pressure-correction scalar back out of a TPressure once it has converged.
public interface IProjectionStrategy<TPressure> : IPressureStrategy<TPressure, float> {

	// Returns a copy of cell with its divergence value set to divergence, ready to be
	// used as the seed for the pressure-correction relaxation loop.
	TPressure SetDivergence(
		GridCell<TPressure> cell,
		float divergence
	);

}
