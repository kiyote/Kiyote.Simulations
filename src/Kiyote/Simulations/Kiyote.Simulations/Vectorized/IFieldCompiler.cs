using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized;

// Moves values between a topology's IGrid<TCell> leaves and a dense Field.
public interface IFieldCompiler {

	Field<TCell> Compile<TCell, TSelector>(
		GridTopology<TCell> topology,
		TSelector selector
	)
		where TSelector : struct, IFieldSelector<TCell>;

	// Writes the field back into its topology's leaves. Every leaf grid must be an
	// IMutableGrid<TCell>.
	void Decompile<TCell, TSelector>(
		Field<TCell> field,
		TSelector selector
	)
		where TSelector : struct, IFieldSelector<TCell>;

}
