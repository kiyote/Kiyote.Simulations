using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmospherePressure {

	// Sums gas into total, then derives pressure from total and temperature.
	void Compute(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float>[] gas,
		IGridLayer<float> temperature,
		IGridLayer<float> total,
		IGridLayer<float> pressure
	);

	// Writes the sum of all gas layers into target for every valid cell of the slot.
	void SumGas(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float>[] gas,
		IGridLayer<float> target
	);

}
