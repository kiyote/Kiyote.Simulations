using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Advection;

public interface IGridAdvection {

	// Transports a scalar field (e.g. gas concentration) along the given velocity
	// field using semi-Lagrangian advection: for each cell, traces backward along
	// velocity by the simulation's fixed time step (per ISimulationClock) to find
	// where its value came from, then samples source at that (possibly fractional)
	// position via sampler.
	//
	// TCell is the type connectivity was built from - the same IConnectivityGrid<TCell>
	// already built for diffusion/pressure/projection over this space can be reused
	// here, since connectivity only tracks which neighbors are open per cell,
	// independent of what value type the grid it was attached to holds.
	//
	// Backtraced positions that land outside the grid's bounds are clamped to the
	// nearest in-bounds position by the sampler; cells with no open neighbors (fully
	// enclosed/solid) are left unchanged rather than sampled.
	void Update<TCell, TValue, TSampler>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> velocity,
		IGrid<TValue> source,
		IMutableGrid<TValue> destination,
		TSampler sampler
	)
		where TSampler : IGridSampler<TValue>;

}
