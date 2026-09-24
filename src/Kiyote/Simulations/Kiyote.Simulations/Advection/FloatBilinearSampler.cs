using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Advection;

// Default IGridSampler<float> implementation for the common case of advecting a
// plain float-valued grid (e.g. gas concentration). Performs bilinear interpolation
// between the four cells surrounding the (possibly fractional) sample position,
// clamping to the grid's bounds so out-of-range backtraced positions still resolve
// to a sensible value instead of throwing.
public sealed class FloatBilinearSampler : IGridSampler<float> {

	float IGridSampler<float>.Sample<TGrid>(
		TGrid grid,
		float column,
		float row
	) {
		(int column0, int column1, float columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, float rowFraction) = Axis( row, grid.Row, grid.Height );

		float topLeft = grid[column0, row0];
		float topRight = grid[column1, row0];
		float bottomLeft = grid[column0, row1];
		float bottomRight = grid[column1, row1];

		float top = Lerp( topLeft, topRight, columnFraction );
		float bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	private static (int Low, int High, float Fraction) Axis(
		float value,
		int origin,
		int length
	) {
		int max = origin + length - 1;
		float clamped = Math.Clamp( value, origin, max );
		int low = Math.Clamp( (int)MathF.Floor( clamped ), origin, max );
		int high = Math.Clamp( low + 1, origin, max );
		float fraction = clamped - low;
		return (low, high, fraction);
	}

	private static float Lerp(
		float left,
		float right,
		float fraction
	) {
		return left + ( ( right - left ) * fraction );
	}

}
