using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereThermal {

	// Mixes arriving heat and conducts across open faces into temperatureNext.
	// Adds the gas vented this step to vented. Returns whether any cell is still changing.
	// temperature, flowEast and flowSouth are padded spans; every other span is a chunk span (see AtmosphereSimd).
	bool Mix(
		ReadOnlySpan<ulong> presence,
		float dt,
		float conduction,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> temperature,
		Span<float> temperatureNext,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> vent,
		ReadOnlySpan<Direction> connectivity,
		ReadOnlySpan<float> outgoing,
		ReadOnlySpan<float> newTotal,
		ref float vented
	);

}
