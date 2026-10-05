using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereCondensation {

	// Exchanges gas and condensate toward each gas's condensation point.
	// Returns whether any cell changed phase by more than epsilon.
	bool Condense(
		int slot,
		ReadOnlySpan<ulong> mask,
		float condensation,
		IGasRegistry gases,
		IGridLayer<float> temperatureNext,
		IGridLayer<float>[] gasNext,
		IGridLayer<float>?[] condensate
	);

}
