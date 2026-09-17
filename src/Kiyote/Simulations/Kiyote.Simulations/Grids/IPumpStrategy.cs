using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Governs how a pump/vent injects or extracts a gas species' partial pressure
// at a given cell for a single tick. Implementations typically only change
// the concentration for the cell(s) they're attached to, returning the value
// unchanged everywhere else, but may model area-effect pumps by evaluating
// any cell in the grid. Implementations are responsible for clamping to a
// sensible minimum (e.g. zero) when extracting.
public interface IPumpStrategy<TCell, TScalar> {

	TScalar Apply(
		GridCell<TCell> cell,
		TScalar concentration,
		double timeStep
	);

}
