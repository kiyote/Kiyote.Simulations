using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IGridThermals {

	IThermal Create<TCell, TStrategy>(
		IGridAssembly<TCell> ship,
		TStrategy strategy
	) where TStrategy : struct, IThermalCellStrategy<TCell>;

}
