using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalRadiation {

	// Applies radiative loss from vacuum-facing faces and absorbed sunlight to temperatureNext in place.
	// All spans are chunk spans (see AtmosphereSimd).
	void Radiate(
		ReadOnlySpan<ulong> presence,
		float dt,
		float stefanBoltzmann,
		float spaceTemperature,
		ReadOnlySpan<float> emissivity,
		ReadOnlySpan<float> capacity,
		ReadOnlySpan<float> solar,
		ReadOnlySpan<Direction> vacuum,
		Span<float> temperatureNext
	);

}
