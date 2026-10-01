using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IGridAtmospherics {

	IAtmosphere Create<TCell, TStrategy>(
		IGridAssembly<TCell> ship,
		TStrategy strategy
	) where TStrategy : struct, IAtmosphereCellStrategy<TCell>;

}
