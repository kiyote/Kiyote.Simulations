using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Projection;

public interface IGridProjection {

	// Projects the given velocity field onto its divergence-free component, removing
	// any non-physical expansion/compression that earlier simulation steps (forces,
	// diffusion, etc.) may have introduced, so the resulting flow conserves mass.
	//
	// Internally this composes IGridPressure/IGridDiffusion to iteratively relax a
	// pressure-correction field to convergence, then subtracts that field's gradient
	// from the source velocity - callers do not need to loop this themselves, unlike
	// the fixed-time-step Update methods on IGridDiffusion/IGridPressure.
	//
	// Velocity represents the shared bulk airflow of the atmosphere as a whole, not
	// any single gas species - unlike IGridPressure/IGridDiffusion, which are called
	// once per gas species to model a mixed atmosphere, IGridProjection only needs to
	// run once per frame against the one velocity field all gas species are advected by.
	//
	// TCell is the type connectivity was built from (e.g. whatever grid encodes wall/
	// permeability layout), letting the same IConnectivityGrid<TCell> already built for
	// pressure/diffusion over this space be reused here rather than requiring a separate
	// IConnectivityGrid<Velocity> - connectivity only tracks which neighbors are open per
	// cell, independent of what value type the grid it was attached to holds.
	//
	// pressureSource/pressureDestination are a pair of TPressure grids, sized to match
	// source/destination, that this simulation uses as scratch space to iteratively
	// relax the divergence-free pressure-correction field (double-buffered between the
	// two grids across iterations, mirroring how IGridDiffusion/IGridPressure double-
	// buffer their own source/destination). Callers do not need to seed them with
	// anything meaningful - their contents are overwritten as part of the solve.
	void Update<TCell, TPressure, TProjectionStrategy>(
		IGrid<Velocity> sourceVelocity,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<Velocity> destinationVelocity,
		IMutableGrid<TPressure> pressureSource,
		IMutableGrid<TPressure> pressureDestination,
		TProjectionStrategy projection
	)
		where TProjectionStrategy : IProjectionStrategy<TPressure>;

}
