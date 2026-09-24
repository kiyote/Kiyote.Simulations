using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Diffusion;

public interface IGridDiffusion {

	void Update<TCell, TValue, TFlow, TFlowStrategy>(
		IGrid<TValue> source,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<TValue> destination,
		TFlowStrategy flow
	)
		where TFlowStrategy : IDiffusionStrategy<TValue, TFlow>;
}
