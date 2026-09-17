using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface IDiffusionStrategy<TCell, TFlow> {

	TFlow CalculateTransfer(
		GridCell<TCell> source,
		GridCell<TCell> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
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
