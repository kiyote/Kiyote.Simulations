using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Diffusion;

public interface IDiffusionStrategy<TCell, TFlow> {

	TFlow CalculateTransfer(
		GridCell<TCell> source,
		GridCell<TCell> destination,
		int sourceNeighborCount,
		int destinationNeighborCount,
		double timeStep
	);

	TFlow Combine(
		TFlow left,
		TFlow right
	);

	TFlow Negate(
		TFlow value
	);

	TCell Apply(
		GridCell<TCell> cell,
		TFlow delta
	);

}
