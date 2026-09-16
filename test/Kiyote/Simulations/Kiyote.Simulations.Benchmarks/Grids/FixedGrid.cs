using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

/// <summary>
/// A dense, fixed-size <see cref="IGrid{T}"/> used to benchmark against a
/// consistent 100x100 layout without the overhead of attach/detach support.
/// </summary>
internal sealed class FixedGrid<T> : IGrid<T> {

	private readonly T[][] _cells;

	public FixedGrid(
		T[][] cells,
		int width,
		int height
	) {
		_cells = cells;
		Width = width;
		Height = height;
	}

	public T? this[int column, int row] {
		get {
			if( column < 0 || column >= Width || row < 0 || row >= Height ) {
				return default;
			}
			return _cells[column][row];
		}
	}

	public int Column => 0;

	public int Row => 0;

	public int Width { get; }

	public int Height { get; }

	public IGrid<T>? GetGrid(
		int column,
		int row
	) {
		return null;
	}

	public IGrid<T>? GetGrid(
		int column,
		int row,
		bool recursive
	) {
		return null;
	}

	public void VisitGrids(
		int column,
		int row,
		Action<IGrid<T>, int, int> visitor
	) {
		ArgumentNullException.ThrowIfNull( visitor );
	}

	public bool TryAttach(
		IGrid<T> grid,
		int column,
		int row
	) {
		return false;
	}

	public bool TryDetach(
		IGrid<T> grid
	) {
		return false;
	}

}
