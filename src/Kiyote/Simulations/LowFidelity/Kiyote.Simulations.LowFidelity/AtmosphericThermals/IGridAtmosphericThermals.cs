using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

public interface IGridAtmosphericThermals {

	// Both subsystems borrow this live 32-cell-chunk compilation; the caller owns commit and disposal.
	IAtmosphericThermals Create<TCell, TAtmosphereStrategy, TThermalStrategy>(
		ICompiledGridAssembly<TCell> compiled,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy
	) where TAtmosphereStrategy : struct, IAtmosphereCellStrategy<TCell>
		where TThermalStrategy : struct, IThermalCellStrategy<TCell>;

}
