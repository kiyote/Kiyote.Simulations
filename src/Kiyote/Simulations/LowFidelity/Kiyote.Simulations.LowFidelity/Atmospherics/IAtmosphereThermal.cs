using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereThermal {

	// Mixes arriving heat and conducts across open faces into temperatureNext.
	// Adds the gas vented this step to vented. Returns whether any cell is still changing.
	bool Mix(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		float conduction,
		IGridLayer<float> total,
		IGridLayer<float> temperature,
		IGridLayer<float> temperatureNext,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> vent,
		IGridLayer<Direction> connectivity,
		ReadOnlySpan<float> outgoing,
		ReadOnlySpan<float> newTotal,
		ref float vented
	);

}
