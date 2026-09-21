using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Diffusion;

public interface IGridDiffusion {

	void Update<TCell, TFlow, TFlowStrategy, TPassability, TSetCell>(
		IGrid<TCell> grid,
		TFlowStrategy strategy,
		TPassability isPassable,
		TSetCell setCell,
		double timeStep
	)
		where TFlowStrategy : IDiffusionStrategy<TCell, TFlow>
		where TPassability : ICellStrategy<TCell, bool>
		where TSetCell : ICallbackStrategy<TCell, TCell>;

}
