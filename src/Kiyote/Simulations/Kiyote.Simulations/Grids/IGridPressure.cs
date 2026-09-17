using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Models a space filled with one or more gas species, each carrying its own
// partial-pressure concentration field on top of an arbitrary TCell grid.
// Diffusion is delegated to IGridDiffusion.Flow, run independently per
// species, so species mix naturally as they each equalize toward their own
// steady state across passable cells. Pumps/vents inject or extract a single
// species' partial pressure at specific cells via Inject.
public interface IGridPressure {

	// Advances every supplied gas species by one diffusion pass. Callers add
	// more GasSpecies entries to model additional gases without any API
	// changes; species diffuse independently of one another so partial
	// pressures mix without any explicit cross-species coupling.
	void Diffuse<TCell, TScalar, TDiffusionStrategy, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TDiffusionStrategy diffusionStrategy,
		TPassability isPassable,
		double timeStep
	)
		where TDiffusionStrategy : IDiffusionStrategy<TScalar, TScalar>
		where TPassability : ICellStrategy<TScalar, bool>
		where TScalarSetCell : ICallbackStrategy<TScalar, TScalar>;

	// Applies the supplied pumps, in order, to a single gas species'
	// concentration grid for a tick. Each pump strategy computes the updated
	// concentration for the cell(s) it cares about (adding for injection,
	// removing for extraction/venting, clamping as appropriate) and leaves
	// every other cell's concentration unchanged. Impassable cells are
	// skipped so pumps can't inject into solid geometry.
	void Inject<TCell, TScalar, TPumpStrategy, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		GasSpecies<TScalar, TScalarSetCell> gas,
		IReadOnlyList<TPumpStrategy> pumps,
		TPassability isPassable,
		double timeStep
	)
		where TPumpStrategy : IPumpStrategy<TCell, TScalar>
		where TPassability : ICellStrategy<TCell, bool>
		where TScalarSetCell : ICallbackStrategy<TScalar, TScalar>;

}
