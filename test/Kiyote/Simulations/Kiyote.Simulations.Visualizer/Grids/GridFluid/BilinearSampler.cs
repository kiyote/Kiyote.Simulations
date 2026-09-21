using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.Grids.GridFluid;

// Bilinear interpolation sampler shared by velocity self-advection and gas advection.
[ExcludeFromCodeCoverage]
internal readonly struct BilinearSampler : IGridSampler<double, Velocity>, IGridSampler<double, double> {

	Velocity IGridSampler<double, Velocity>.Sample(
		IGrid<Velocity> grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		Velocity topLeft = grid[column0, row0];
		Velocity topRight = grid[column1, row0];
		Velocity bottomLeft = grid[column0, row1];
		Velocity bottomRight = grid[column1, row1];

		Velocity top = Lerp( topLeft, topRight, columnFraction );
		Velocity bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	double IGridSampler<double, double>.Sample(
		IGrid<double> grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		double topLeft = grid[column0, row0];
		double topRight = grid[column1, row0];
		double bottomLeft = grid[column0, row1];
		double bottomRight = grid[column1, row1];

		double top = Lerp( topLeft, topRight, columnFraction );
		double bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	// Generic overloads: let struct-based IGrid<T> implementations (e.g. VelocityGridView) be
	// sampled without boxing, since grid is used through the TGrid constraint directly instead
	// of being converted to the IGrid<T> interface type.
	Velocity IGridSampler<double, Velocity>.Sample<TGrid>(
		TGrid grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		Velocity topLeft = grid[column0, row0];
		Velocity topRight = grid[column1, row0];
		Velocity bottomLeft = grid[column0, row1];
		Velocity bottomRight = grid[column1, row1];

		Velocity top = Lerp( topLeft, topRight, columnFraction );
		Velocity bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	double IGridSampler<double, double>.Sample<TGrid>(
		TGrid grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		double topLeft = grid[column0, row0];
		double topRight = grid[column1, row0];
		double bottomLeft = grid[column0, row1];
		double bottomRight = grid[column1, row1];

		double top = Lerp( topLeft, topRight, columnFraction );
		double bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	private static (int Low, int High, double Fraction) Axis(
		double value,
		int origin,
		int length
	) {
		int max = origin + length - 1;
		double clamped = Math.Clamp( value, origin, max );
		int low = Math.Clamp( (int)Math.Floor( clamped ), origin, max );
		int high = Math.Clamp( low + 1, origin, max );
		double fraction = clamped - low;
		return (low, high, fraction);
	}

	private static Velocity Lerp(
		Velocity left,
		Velocity right,
		double fraction
	) {
		return left + ( ( right - left ) * fraction );
	}

	private static double Lerp(
		double left,
		double right,
		double fraction
	) {
		return left + ( ( right - left ) * fraction );
	}

}
