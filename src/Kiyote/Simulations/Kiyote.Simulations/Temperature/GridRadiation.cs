using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Temperature;

public sealed class GridRadiation : IGridRadiation {

	void IGridRadiation.Update<TCell, TExposure, TStrategy, TSetCell>(
		IGrid<TCell> grid,
		TExposure isExposed,
		TStrategy strategy,
		TSetCell setCell,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );

		int left = grid.Column;
		int top = grid.Row;
		int width = grid.Width;
		int height = grid.Height;

		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				GridCell<TCell> cell = new( column, row, grid[column, row] );

				if( !isExposed.Evaluate( cell ) ) {
					continue;
				}

				TCell updated = strategy.Apply( cell, timeStep );
				setCell.Callback( cell, updated );
			}
		}
	}

}
