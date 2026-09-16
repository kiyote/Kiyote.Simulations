using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer;

[ExcludeFromCodeCoverage]
internal readonly struct AlwaysPassableStrategy : ICellStrategy<byte, bool> {

	public bool Evaluate(
		GridCell<byte> cell
	) {
		return true;
	}

}
