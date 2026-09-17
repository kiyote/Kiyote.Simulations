using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Couples IGridPressure's partial-pressure fields to IGridFluid's bulk-velocity
// engine so pressure imbalances (e.g. across a newly-opened door) drive fast,
// realistic bulk airflow instead of relying on slow molecular diffusion alone.
// Pumps/vents and residual species diffusion are still applied by the caller
// via IGridPressure.Inject/Diffuse - GridAirflow only owns the pressure-to-
// velocity coupling and the velocity step/advection pass.
public interface IGridAirflow {

	// Reads a precomputed total-pressure field (the sum of every gas species'
	// partial pressure at each cell) and applies a Velocity impulse to every
	// passable cell, proportional to the pressure difference across each of
	// its passable neighbors, as computed by the supplied strategy.
	void ApplyPressureForcing<TCell, TVelocityAccessor, TForcingStrategy, TBoundary, TCellSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IGrid<double> totalPressure,
		TForcingStrategy forcingStrategy,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	)
		where TVelocityAccessor : IVelocityAccessor<TCell>
		where TForcingStrategy : IPressureForcingStrategy
		where TBoundary : IBoundaryStrategy
		where TCellSetCell : ICallbackStrategy<TCell, TCell>;

	// One full airflow tick for a single cell/velocity grid: applies pressure
	// forcing from the supplied total-pressure field, then advances velocity
	// (diffuse/project/advect/project) and advects every supplied gas species
	// along the resulting velocity field.
	void Advance<TCell, TVelocityAccessor, TForcingStrategy, TVelocityDiffusion, TScalar, TSampler, TBoundary, TCellSetCell, TScalarSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IGrid<double> totalPressure,
		TForcingStrategy forcingStrategy,
		TVelocityDiffusion velocityDiffusion,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TSampler sampler,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	)
		where TVelocityAccessor : IVelocityAccessor<TCell>
		where TForcingStrategy : IPressureForcingStrategy
		where TVelocityDiffusion : IDiffusionStrategy<Velocity, Velocity>
		where TSampler : IGridSampler<double, Velocity>, IGridSampler<double, TScalar>
		where TBoundary : IBoundaryStrategy
		where TCellSetCell : ICallbackStrategy<TCell, TCell>
		where TScalarSetCell : ICallbackStrategy<TScalar, TScalar>;

}
