using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal interface IAtmosphereAcceleration {

	// Accelerates east/south face flows and vacuum vents from the pressure field.
	// pressure is a padded span; every other span is a chunk span (see AtmosphereSimd).
	// eastOpen/southOpen say whether the last column/row may flow into the neighbouring chunk.
	void Accelerate(
		ReadOnlySpan<ulong> presence,
		float k,
		float damp,
		bool eastOpen,
		bool southOpen,
		ReadOnlySpan<float> pressure,
		Span<float> flowEast,
		Span<float> flowSouth,
		Span<float> vent,
		ReadOnlySpan<Direction> connectivity,
		ReadOnlySpan<Direction> vacuum,
		ReadOnlySpan<bool> permeable
	);

}
