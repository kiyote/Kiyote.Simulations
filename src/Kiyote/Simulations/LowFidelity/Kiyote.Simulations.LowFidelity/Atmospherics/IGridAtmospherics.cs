using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IGridAtmospherics {

	// Borrows a live compilation with 32-cell chunks; the caller owns commit and disposal.
	IAtmosphere Create<TCell, TStrategy>(
		ICompiledGridAssembly<TCell> compiled,
		TStrategy strategy
	) where TStrategy : struct, IAtmosphereCellStrategy<TCell>;

}
