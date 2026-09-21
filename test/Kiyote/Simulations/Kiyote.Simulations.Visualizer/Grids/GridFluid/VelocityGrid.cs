using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.Grids.GridFluid;

/// <summary>
/// A dense, fixed-size <see cref="IGrid{T}"/> of <see cref="Velocity"/> values used to
/// drive the GridFluid visualizer. Velocity isn't an INumber, so this can't reuse the
/// GridDiffusion BufferGrid&lt;T&gt;.
/// </summary>
internal sealed class VelocityGrid : IGrid<Velocity> {

	private readonly Velocity[][] _cells;

	public VelocityGrid(
		int width,
		int height
	) {
		Width = width;
		Height = height;
		_cells = new Velocity[height][];
		for( int row = 0; row < height; row++ ) {
			_cells[row] = new Velocity[width];
		}
	}

	public int Column => 0;

	public int Row => 0;

	public int Width { get; }

	public int Height { get; }

	public Velocity this[int column, int row] => _cells[row][column];

	public void Set(
		int column,
		int row,
		Velocity value
	) {
		_cells[row][column] = value;
	}

	public IGrid<Velocity>? GetGrid(
		int column,
		int row
	) {
		throw new NotImplementedException();
	}

	public IGrid<Velocity>? GetGrid(
		int column,
		int row,
		bool recursive
	) {
		throw new NotImplementedException();
	}

	public void VisitGrids(
		int column,
		int row,
		Action<IGrid<Velocity>, int, int> visitor
	) {
		throw new NotImplementedException();
	}

	public bool TryAttach(
		IGrid<Velocity> grid,
		int column,
		int row
	) {
		throw new NotImplementedException();
	}

	public bool TryDetach(
		IGrid<Velocity> grid
	) {
		throw new NotImplementedException();
	}

}
