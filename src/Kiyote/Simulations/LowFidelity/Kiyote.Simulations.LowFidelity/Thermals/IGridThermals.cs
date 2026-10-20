using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IGridThermals {

	// Borrows a live compilation with 32-cell chunks; the caller owns commit and disposal.
	IThermal Create<TCell, TStrategy>(
		ICompiledGridAssembly<TCell> compiled,
		TStrategy strategy
	) where TStrategy : struct, IThermalCellStrategy<TCell>;

}
