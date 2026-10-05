using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalRadiation {

	// Applies radiative loss from vacuum-facing faces and absorbed sunlight to temperatureNext in place.
	void Radiate(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		float stefanBoltzmann,
		float spaceTemperature,
		IGridLayer<float> emissivity,
		IGridLayer<float> capacity,
		IGridLayer<float> solar,
		IGridLayer<Direction> vacuum,
		IGridLayer<float> temperatureNext
	);

}
