using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IGridAtmospherics<TCell> {

	void Update(
		IGrid<TCell> grid,
		IConnectivityGrid<TCell> connectivity
	);

}
