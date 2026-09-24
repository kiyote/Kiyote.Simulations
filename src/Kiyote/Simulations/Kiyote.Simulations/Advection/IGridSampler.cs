using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Advection;

// Samples a value out of a grid at a fractional (non-integer) column/row position,
// letting semi-Lagrangian advection look up a value at an arbitrary backtraced
// location rather than only at whole-number cell coordinates.
//
// The generic Sample<TGrid> overload lets struct-based IGrid<TValue> implementations
// be sampled without boxing, since grid is used through the TGrid constraint directly
// instead of being converted to the IGrid<TValue> interface type.
public interface IGridSampler<TValue> {

	TValue Sample<TGrid>(
		TGrid grid,
		float column,
		float row
	)
		where TGrid : IGrid<TValue>;

}
