using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface ISealedAnalyzer {

	bool IsSealed<TCell, TPassability>(
		IGrid<TCell> grid,
		int startColumn,
		int startRow,
		TPassability isPassable
	)
		where TPassability : ICellStrategy<TCell, bool>;

}
