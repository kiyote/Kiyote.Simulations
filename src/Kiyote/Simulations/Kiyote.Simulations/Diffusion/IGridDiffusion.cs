using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Diffusion;

public interface IGridDiffusion {

	void Update<TCell, TValue, TFlow, TDiffusionStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TValue> source,
		IMutableGrid<TValue> destination,
		TDiffusionStrategy diffusionStrategy
	)
		where TDiffusionStrategy : IDiffusionStrategy<TValue, TFlow>;
}
