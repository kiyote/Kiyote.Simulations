using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Temperature;

public interface IGridRadiation {

	void Update<TCell, TExposure, TStrategy, TSetCell>(
		IGrid<TCell> grid,
		TExposure isExposed,
		TStrategy strategy,
		TSetCell setCell,
		double timeStep
	)
		where TExposure : ICellStrategy<TCell, bool>
		where TStrategy : IRadiativeLossStrategy<TCell>
		where TSetCell : ICallbackStrategy<TCell, TCell>;

}
