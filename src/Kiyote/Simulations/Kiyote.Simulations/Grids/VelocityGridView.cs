using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// A read-only overlay that exposes just the Velocity component of an arbitrary
// IGrid<TCell>, so IGridFluid can operate on Velocity, IDiffusionStrategy<Velocity,Velocity>,
// and IGridSampler<double,Velocity> without requiring TCell to be Velocity itself.
public readonly struct VelocityGridView<TCell, TVelocityAccessor> : IGrid<Velocity>, IEquatable<VelocityGridView<TCell, TVelocityAccessor>>
	where TVelocityAccessor : IVelocityAccessor<TCell> {

	private readonly IGrid<TCell> _grid;
	private readonly TVelocityAccessor _accessor;

	public VelocityGridView(
		IGrid<TCell> grid,
		TVelocityAccessor accessor
	) {
		_grid = grid;
		_accessor = accessor;
	}

	public int Column => _grid.Column;

	public int Row => _grid.Row;

	public int Width => _grid.Width;

	public int Height => _grid.Height;

	public Velocity this[int column, int row] => _accessor.GetVelocity( _grid[column, row]! );

	public IGrid<Velocity>? GetGrid(
		int column,
		int row
	) {
		return null;
	}

	public IGrid<Velocity>? GetGrid(
		int column,
		int row,
		bool recursive
	) {
		return null;
	}

	public void VisitGrids(
		int column,
		int row,
		Action<IGrid<Velocity>, int, int> visitor
	) {
		ArgumentNullException.ThrowIfNull( visitor );
	}

	public bool TryAttach(
		IGrid<Velocity> grid,
		int column,
		int row
	) {
		return false;
	}

	public bool TryDetach(
		IGrid<Velocity> grid
	) {
		return false;
	}

	public bool Equals(
		VelocityGridView<TCell, TVelocityAccessor> other
	) {
		return Equals( _grid, other._grid ) && EqualityComparer<TVelocityAccessor>.Default.Equals( _accessor, other._accessor );
	}

	public override bool Equals(
		object? obj
	) {
		return obj is VelocityGridView<TCell, TVelocityAccessor> other && Equals( other );
	}

	public override int GetHashCode() {
		return HashCode.Combine( _grid, _accessor );
	}

	public static bool operator ==(
		VelocityGridView<TCell, TVelocityAccessor> left,
		VelocityGridView<TCell, TVelocityAccessor> right
	) {
		return left.Equals( right );
	}

	public static bool operator !=(
		VelocityGridView<TCell, TVelocityAccessor> left,
		VelocityGridView<TCell, TVelocityAccessor> right
	) {
		return !left.Equals( right );
	}

}

