using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// A partial-pressure atmosphere engine: each gas species diffuses on its own
// concentration field via the shared IGridDiffusion.Flow neighbor-walk, so
// species mix without any explicit cross-species coupling. Pumps/vents then
// add or remove a single species' partial pressure at specific cells.
public sealed class GridPressure : IGridPressure {

	private readonly IGridDiffusion _gridDiffusion;

	public GridPressure(
		IGridDiffusion gridDiffusion
	) {
		_gridDiffusion = gridDiffusion;
	}

	void IGridPressure.Diffuse<TCell, TScalar, TDiffusionStrategy, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TDiffusionStrategy diffusionStrategy,
		TPassability isPassable,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );
		ArgumentNullException.ThrowIfNull( gases );

		for( int i = 0; i < gases.Count; i++ ) {
			GasSpecies<TScalar, TScalarSetCell> gas = gases[i];
			_gridDiffusion.Flow<TScalar, TScalar, TDiffusionStrategy, TPassability, TScalarSetCell>( gas.Concentration, diffusionStrategy, isPassable, gas.SetCell );
		}
	}

	void IGridPressure.Inject<TCell, TScalar, TPumpStrategy, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		GasSpecies<TScalar, TScalarSetCell> gas,
		IReadOnlyList<TPumpStrategy> pumps,
		TPassability isPassable,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );
		ArgumentNullException.ThrowIfNull( pumps );

		int left = grid.Column;
		int top = grid.Row;
		int width = grid.Width;
		int height = grid.Height;

		IGrid<TScalar> concentration = gas.Concentration;

		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				GridCell<TCell> cell = new( column, row, grid[column, row] );

				if( !isPassable.Evaluate( cell ) ) {
					continue;
				}

				TScalar current = concentration[column, row]!;
				TScalar updated = current;
				for( int i = 0; i < pumps.Count; i++ ) {
					updated = pumps[i].Apply( cell, updated, timeStep );
				}

				if( EqualityComparer<TScalar>.Default.Equals( updated, current ) ) {
					continue;
				}

				gas.SetCell.Callback( new GridCell<TScalar>( column, row, current ), updated );
			}
		}
	}

}
