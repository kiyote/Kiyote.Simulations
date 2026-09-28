using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Visualizer;

internal sealed class BoundaryFloatConnectivityStrategy : IConnectivityStrategy<float> {

	private readonly int _left;
	private readonly int _top;
	private readonly int _width;
	private readonly int _height;

	public BoundaryFloatConnectivityStrategy(
		int left,
		int top,
		int width,
		int height
	) {
		_left = left;
		_top = top;
		_width = width;
		_height = height;
	}

	bool IConnectivityStrategy<float>.Evaluate(
		GridCell<float> source,
		GridCell<float> destination,
		Direction direction,
		GridCell<float> orthogonalA,
		GridCell<float> orthogonalB
	) {
		return IsPassable( source ) && IsPassable( destination );
	}

	private bool IsPassable(
		GridCell<float> cell
	) {
		// The outermost ring of the rectangle (all four sides) is the wall.
		return cell.Column > _left
			&& cell.Column < _left + _width - 1
			&& cell.Row > _top
			&& cell.Row < _top + _height - 1;
	}
}
