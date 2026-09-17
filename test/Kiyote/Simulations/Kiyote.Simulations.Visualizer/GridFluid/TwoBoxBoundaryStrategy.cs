using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridFluid;

// Describes two rectangular chambers connected by a narrow corridor; everything
// outside of those three regions is treated as solid hull.
[ExcludeFromCodeCoverage]
internal readonly struct TwoBoxBoundaryStrategy : IBoundaryStrategy {

	private readonly (int Left, int Top, int Right, int Bottom) _boxA;
	private readonly (int Left, int Top, int Right, int Bottom) _boxB;
	private readonly (int Left, int Top, int Right, int Bottom) _corridor;

	public TwoBoxBoundaryStrategy(
		(int Left, int Top, int Right, int Bottom) boxA,
		(int Left, int Top, int Right, int Bottom) boxB,
		(int Left, int Top, int Right, int Bottom) corridor
	) {
		_boxA = boxA;
		_boxB = boxB;
		_corridor = corridor;
	}

	public bool IsSolid(
		int column,
		int row
	) {
		return !Contains( _boxA, column, row )
			&& !Contains( _boxB, column, row )
			&& !Contains( _corridor, column, row );
	}

	// Bounces velocity off the hull elastically: the requesting cell's own velocity
	// is inverted, so momentum heading into a wall is reflected back into the
	// chamber instead of being absorbed/vanished.
	public Velocity ReflectVelocity(
		Velocity velocity,
		int column,
		int row
	) {
		return -velocity;
	}

	private static bool Contains(
		(int Left, int Top, int Right, int Bottom) bounds,
		int column,
		int row
	) {
		return column >= bounds.Left
			&& column <= bounds.Right
			&& row >= bounds.Top
			&& row <= bounds.Bottom;
	}

}
