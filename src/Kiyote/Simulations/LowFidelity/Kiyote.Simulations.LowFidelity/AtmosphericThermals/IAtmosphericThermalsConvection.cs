using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal interface IAtmosphericThermalsConvection {

	void Exchange(
		ReadOnlySpan<ConvectionCell> cells,
		ReadOnlySpan<ConvectionLink> links,
		float dt,
		float gasHeatCapacity,
		IAtmosphere atmosphere,
		IThermal thermal
	);

}
