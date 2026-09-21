using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Temperature;

public interface IGridTemperature {

	void Update<TCell, TFlow, TDiffusionStrategy, TPassability, TExposure, TRadiationStrategy, TSetCell>(
		IGrid<TCell> grid,
		TDiffusionStrategy diffusionStrategy,
		TPassability isPassable,
		TExposure isExposed,
		TRadiationStrategy radiationStrategy,
		TSetCell setCell,
		double timeStep
	)
		where TDiffusionStrategy : IDiffusionStrategy<TCell, TFlow>
		where TPassability : ICellStrategy<TCell, bool>
		where TExposure : ICellStrategy<TCell, bool>
		where TRadiationStrategy : IRadiativeLossStrategy<TCell>
		where TSetCell : ICallbackStrategy<TCell, TCell>;

}
