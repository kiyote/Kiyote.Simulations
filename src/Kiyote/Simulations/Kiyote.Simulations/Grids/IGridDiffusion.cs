using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface IGridDiffusion {

	void Flow<TCell, TFlow, TFlowStrategy, TPassability, TSetCell>(
		IGrid<TCell> grid,
		TFlowStrategy strategy,
		TPassability isPassable,
		TSetCell setCell
	)
		where TFlowStrategy : IDiffusionStrategy<TCell, TFlow>
		where TPassability : ICellStrategy<TCell, bool>
		where TSetCell : ICallbackStrategy<TCell, TCell>;

}
