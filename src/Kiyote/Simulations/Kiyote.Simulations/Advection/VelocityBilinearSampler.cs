using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Advection;

// Default IGridSampler<Velocity> implementation, used for self-advecting a velocity
// field along itself. Performs bilinear interpolation between the four cells
// surrounding the (possibly fractional) sample position, independently for each of
// Velocity's X and Y components, clamping to the grid's bounds so out-of-range
// backtraced positions still resolve to a sensible value instead of throwing.
public sealed class VelocityBilinearSampler : IGridSampler<Velocity> {

	Velocity IGridSampler<Velocity>.Sample<TGrid>(
		TGrid grid,
		float column,
		float row
	) {
		(int column0, int column1, float columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, float rowFraction) = Axis( row, grid.Row, grid.Height );

		Velocity topLeft = grid[column0, row0];
		Velocity topRight = grid[column1, row0];
		Velocity bottomLeft = grid[column0, row1];
		Velocity bottomRight = grid[column1, row1];

		Velocity top = Lerp( topLeft, topRight, columnFraction );
		Velocity bottom = Lerp( bottomLeft, bottomRight, columnFraction );
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

	private static Velocity Lerp(
		Velocity left,
		Velocity right,
		float fraction
	) {
		return new Velocity(
			left.X + ( ( right.X - left.X ) * fraction ),
			left.Y + ( ( right.Y - left.Y ) * fraction )
		);
	}

}
