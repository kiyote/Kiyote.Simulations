using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer.Temperature;

/// <summary>
/// Passable wherever the shape mask marks a cell as steel, allowing irregularly
/// shaped blocks (not just rectangular bounds) to be modeled.
/// </summary>
[ExcludeFromCodeCoverage]
internal readonly struct SteelPassabilityStrategy : ICellStrategy<double, bool> {

	private readonly bool[][] _mask;

	public SteelPassabilityStrategy(
		bool[][] mask
	) {
		_mask = mask;
	}

	public bool Evaluate(
		GridCell<double> cell
	) {
		return _mask[cell.Column][cell.Row];
	}

}
