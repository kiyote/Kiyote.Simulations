using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface IGridFlow {

	void Flow<TCell, TFlow, TFlowStrategy, TPassability, TSetCell>(
		IGrid<TCell> grid,
		TFlowStrategy strategy,
		TPassability isPassable,
		TSetCell setCell
	)
		where TFlowStrategy : IFlowStrategy<TCell, TFlow>
		where TPassability : ICellStrategy<TCell, bool>
		where TSetCell : ICallbackStrategy<TCell, TCell>;

}
