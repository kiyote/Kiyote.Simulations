
using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

/// <summary>
/// A leaf <see cref="IGrid{T}"/> backed by a dense array.  It holds values but
/// cannot host other grids.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestGrid<TValue> : IGrid<TValue> {

	private readonly Dictionary<(int Column, int Row), TValue> _cells;

	public TestGrid(
		int column,
		int row,
		int width,
		int height
	) {
		Column = column;
		Row = row;
		Width = width;
		Height = height;
		_cells = [];
	}

	public int Column { get; }

	public int Row { get; }

	public int Width { get; }

	public int Height { get; }

	public TValue this[int column, int row] {
		get {
			if( !Contains( column, row ) ) {
				return default;
			}
			return _cells.TryGetValue( (column, row), out TValue value ) ? value : default;
		}
	}

	public void Set(
		int column,
		int row,
		TValue value
	) {
		_cells[(column, row)] = value;
	}

	public IGrid<TValue> GetGrid(
		int column,
		int row
	) {
		return null;
	}

	public IGrid<TValue> GetGrid(
		int column,
		int row,
		bool recursive
	) {
		return null;
	}

	public void VisitGrids(
		int column,
		int row,
		Action<IGrid<TValue>, int, int> visitor
	) {
		ArgumentNullException.ThrowIfNull( visitor );
	}

	public bool TryAttach(
		IGrid<TValue> grid,
		int column,
		int row
	) {
		return false;
	}

	public bool TryDetach(
		IGrid<TValue> grid
	) {
		return false;
	}

	private bool Contains(
		int column,
		int row
	) {
		return column >= Column
			&& column < Column + Width
			&& row >= Row
			&& row < Row + Height;
	}
}
