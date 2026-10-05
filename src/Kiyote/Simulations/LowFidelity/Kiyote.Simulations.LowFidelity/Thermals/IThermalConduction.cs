using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalConduction {

	// Writes temperatureNext from temperature plus heat exchanged across faces shared with
	// occupied neighbours. Unoccupied cells have zero capacity and exchange nothing.
	void Conduct(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float> temperature,
		IGridLayer<float> conductivity,
		IGridLayer<float> capacity,
		IGridLayer<float> temperatureNext
	);

}
