using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereTransport {

	// Moves each gas along the limited flows into gasNext.
	// Fills outgoing and newTotal (chunk-local, ChunkSize * ChunkSize) for the thermal stage.
	void Transport(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float>[] gas,
		IGridLayer<float>[] gasNext,
		IGridLayer<float> total,
		IGridLayer<float> flowEast,
		IGridLayer<float> flowSouth,
		IGridLayer<float> vent,
		Span<float> outgoing,
		Span<float> newTotal
	);

}
