using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface IFlowStrategy<TCell, TFlow> {

	TFlow CalculateTransfer(
		GridCell<TCell> source,
		GridCell<TCell> destination
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
