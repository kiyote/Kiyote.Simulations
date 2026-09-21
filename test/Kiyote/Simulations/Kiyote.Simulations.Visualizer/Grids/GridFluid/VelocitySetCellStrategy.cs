using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.Grids.GridFluid;

[ExcludeFromCodeCoverage]
internal readonly struct VelocitySetCellStrategy : ICallbackStrategy<Velocity, Velocity> {

	private readonly VelocityGrid _grid;

	public VelocitySetCellStrategy(
		VelocityGrid grid
	) {
		_grid = grid;
	}

	public void Callback(
		GridCell<Velocity> cell,
		Velocity value
	) {
		_grid.Set( cell.Column, cell.Row, value );
	}

}
