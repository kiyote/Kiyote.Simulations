using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

public interface IGridFluid {

	// Advances the velocity component of an arbitrary TCell grid only (diffuse, project, advect, project).
	// TVelocityAccessor lets the grid's cell type carry velocity alongside whatever else the
	// caller's simulation needs (pressure, temperature, material, etc).
	void StepVelocity<TCell, TVelocityAccessor, TVelocityDiffusion, TSampler, TBoundary, TCellSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		TVelocityDiffusion velocityDiffusion,
		TSampler sampler,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	)
		where TVelocityAccessor : IVelocityAccessor<TCell>
		where TVelocityDiffusion : IDiffusionStrategy<Velocity, Velocity>
		where TSampler : IGridSampler<double, Velocity>
		where TBoundary : IBoundaryStrategy
		where TCellSetCell : ICallbackStrategy<TCell, TCell>;

	// Advects every supplied gas species along the current velocity field carried by an
	// arbitrary TCell grid. Callers add more GasSpecies entries to model additional gases
	// without any API changes. Concentration is confined to passable cells: solid cells
	// are always zeroed and never contribute to what passable cells sample. TPassability
	// only needs to know which cells are solid - an IBoundaryStrategy satisfies it too,
	// but callers may supply a narrower passability-only strategy if they don't otherwise
	// need velocity-reflection behavior.
	void AdvectGases<TCell, TVelocityAccessor, TScalar, TSampler, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TSampler sampler,
		TPassability passability,
		double timeStep
	)
		where TVelocityAccessor : IVelocityAccessor<TCell>
		where TSampler : IGridSampler<double, TScalar>
		where TPassability : IGridPassability
		where TScalarSetCell : ICallbackStrategy<TScalar, TScalar>;

}

