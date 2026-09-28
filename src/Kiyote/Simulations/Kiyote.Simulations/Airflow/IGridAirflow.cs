using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Advection;
using Kiyote.Simulations.Pressure;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Airflow;

// Layers Pressure, Projection, and Advection into a single fixed-time-step airflow
// update: builds/relaxes a pressure field for one gas species, projects the shared
// bulk velocity field onto its divergence-free component using that solve, then
// advects the gas species' concentration along the resulting velocity.
//
// Implementations compose their IGridPressure/IGridProjection/IGridAdvection
// instances internally (constructor-injected); Update only accepts the per-call
// grids and strategy structs those layers need, mirroring how each layer already
// takes its strategy as a generic struct parameter.
//
// As with IGridPressure/IGridDiffusion, this models a single gas species per call -
// to model a mixed atmosphere, call this once per species (each with its own
// pressure/concentration source/destination grids), reusing the same connectivity
// and velocity grids for all of them, since velocity represents the shared bulk
// airflow rather than any single species. To advance by a larger duration, call
// this repeatedly, double-buffering source/destination between calls.
public interface IGridAirflow {

	void Update<TCell, TPressure, TFlow, TValue, TPressureStrategy, TProjectionStrategy, TSampler>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TPressure> pressureSource,
		IMutableGrid<TPressure> pressureDestination,
		TPressureStrategy pressureStrategy,
		IGrid<Velocity> sourceVelocity,
		IMutableGrid<Velocity> destinationVelocity,
		IMutableGrid<TPressure> projectionPressureSource,
		IMutableGrid<TPressure> projectionPressureDestination,
		IMutableGrid<float> projectionDivergence,
		TProjectionStrategy projectionStrategy,
		IGrid<TValue> concentrationSource,
		IMutableGrid<TValue> concentrationDestination,
		TSampler sampler
	)
		where TPressureStrategy : IPressureStrategy<TPressure, TFlow>
		where TProjectionStrategy : IProjectionStrategy<TPressure>
		where TSampler : IGridSampler<TValue>;

}
