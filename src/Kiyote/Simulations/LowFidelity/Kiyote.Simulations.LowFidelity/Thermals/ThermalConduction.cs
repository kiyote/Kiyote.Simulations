using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class ThermalConduction : IThermalConduction {

	// Caps each face's exchange so four faces together can never overshoot equilibrium.
	private const float MaximumFaceFraction = 0.25f;

	void IThermalConduction.Conduct(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float> temperatureLayer,
		IGridLayer<float> conductivityLayer,
		IGridLayer<float> capacityLayer,
		IGridLayer<float> temperatureNextLayer
	) {
		int stride = temperatureLayer.Stride;
		Span<float> temperature = temperatureLayer.Cells;
		Span<float> conductivity = conductivityLayer.Cells;
		Span<float> capacity = capacityLayer.Cells;
		Span<float> next = temperatureNextLayer.Cells;

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			int start = temperatureLayer.IndexOf( slot, 0, row );
			for( int column = 0; column < ChunkSize; column++ ) {
				int i = start + column;
				if( ( bits & ( 1u << column ) ) == 0 || capacity[i] <= 0.0f ) {
					next[i] = temperature[i];
					continue;
				}
				float energy = Exchange( i, i - stride, dt, temperature, conductivity, capacity )
					+ Exchange( i, i + 1, dt, temperature, conductivity, capacity )
					+ Exchange( i, i + stride, dt, temperature, conductivity, capacity )
					+ Exchange( i, i - 1, dt, temperature, conductivity, capacity );
				next[i] = temperature[i] + ( energy / capacity[i] );
			}
		}
	}

	// Joules flowing from neighbour into self this step.
	private static float Exchange(
		int self,
		int neighbour,
		float dt,
		Span<float> temperature,
		Span<float> conductivity,
		Span<float> capacity
	) {
		float otherCapacity = capacity[neighbour];
		float a = conductivity[self];
		float b = conductivity[neighbour];
		if( otherCapacity <= 0.0f || a + b <= 0.0f ) {
			return 0.0f;
		}
		// Series conductance of the two half-cells.
		float conductance = 2.0f * a * b / ( a + b ) * dt;
		float limit = MaximumFaceFraction * MathF.Min( capacity[self], otherCapacity );
		return MathF.Min( conductance, limit ) * ( temperature[neighbour] - temperature[self] );
	}

}
