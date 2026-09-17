using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer.GridDiffusion;

[ExcludeFromCodeCoverage]
internal readonly struct AlwaysPassableStrategy : ICellStrategy<double, bool> {

	public bool Evaluate(
		GridCell<double> cell
	) {
		return true;
	}

}
