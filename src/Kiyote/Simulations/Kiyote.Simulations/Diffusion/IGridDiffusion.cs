using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Diffusion;

public interface IGridDiffusion {

	void Update<TCell, TValue, TFlow, TFlowStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TValue> source,
		IMutableGrid<TValue> destination,
		TFlowStrategy flow
	)
		where TFlowStrategy : IDiffusionStrategy<TValue, TFlow>;
}
