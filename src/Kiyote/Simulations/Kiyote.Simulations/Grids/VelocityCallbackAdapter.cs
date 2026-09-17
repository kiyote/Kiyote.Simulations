using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Adapts a Velocity write-back (produced by IGridFluid's internal Velocity math) into a
// write against the caller's real TCell grid, using the caller-supplied accessor and
// their own ICallbackStrategy<TCell,TCell>.
public readonly struct VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> : ICallbackStrategy<Velocity, Velocity>, IEquatable<VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell>>
	where TVelocityAccessor : IVelocityAccessor<TCell>
	where TCellSetCell : ICallbackStrategy<TCell, TCell> {

	private readonly IGrid<TCell> _grid;
	private readonly TVelocityAccessor _accessor;
	private readonly TCellSetCell _setCell;

	public VelocityCallbackAdapter(
		IGrid<TCell> grid,
		TVelocityAccessor accessor,
		TCellSetCell setCell
	) {
		_grid = grid;
		_accessor = accessor;
		_setCell = setCell;
	}

	public void Callback(
		GridCell<Velocity> cell,
		Velocity value
	) {
		TCell current = _grid[cell.Column, cell.Row]!;
		GridCell<TCell> source = new( cell.Column, cell.Row, current );
		TCell updated = _accessor.WithVelocity( current, value );
		_setCell.Callback( source, updated );
	}

	public bool Equals(
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> other
	) {
		return Equals( _grid, other._grid )
			&& EqualityComparer<TVelocityAccessor>.Default.Equals( _accessor, other._accessor )
			&& EqualityComparer<TCellSetCell>.Default.Equals( _setCell, other._setCell );
	}

	public override bool Equals(
		object? obj
	) {
		return obj is VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> other && Equals( other );
	}

	public override int GetHashCode() {
		return HashCode.Combine( _grid, _accessor, _setCell );
	}

	public static bool operator ==(
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> left,
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> right
	) {
		return left.Equals( right );
	}

	public static bool operator !=(
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> left,
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> right
	) {
		return !left.Equals( right );
	}

}

