using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Diffusion;

public interface IDiffusionStrategy<TValue, TFlow> {

	TFlow CalculateTransfer(
		GridCell<TValue> source,
		GridCell<TValue> destination,
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

	TValue Apply(
		GridCell<TValue> cell,
		TFlow delta
	);

}
