using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer.Temperature;

/// <summary>
/// A steel cell is exposed to space when at least one of its orthogonal
/// neighbors falls outside the shape mask, so an irregular boundary (concave
/// notches, convex points, interior cavities) radiates unevenly.
/// </summary>
[ExcludeFromCodeCoverage]
internal readonly struct ExposedToSpaceStrategy : ICellStrategy<double, bool> {

	private readonly bool[][] _mask;

	public ExposedToSpaceStrategy(
		bool[][] mask
	) {
		_mask = mask;
	}

	public bool Evaluate(
		GridCell<double> cell
	) {
		if( !_mask[cell.Column][cell.Row] ) {
			return false;
		}

		return !_mask[cell.Column - 1][cell.Row]
			|| !_mask[cell.Column + 1][cell.Row]
			|| !_mask[cell.Column][cell.Row - 1]
			|| !_mask[cell.Column][cell.Row + 1];
	}

}
