using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Diffusion;

public interface IGridDiffusion {

	void Update<TCell, TFlow, TFlowStrategy>(
		IGrid<TCell> source,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<TCell> destination,
		TFlowStrategy flow
	)
		where TFlowStrategy : IDiffusionStrategy<TCell, TFlow>;
}
