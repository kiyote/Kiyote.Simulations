using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

public interface IGridAtmosphericThermals {

	IAtmosphericThermals Create<TCell, TAtmosphereStrategy, TThermalStrategy>(
		IGridAssembly<TCell> ship,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy
	) where TAtmosphereStrategy : struct, IAtmosphereCellStrategy<TCell>
		where TThermalStrategy : struct, IThermalCellStrategy<TCell>;

}
