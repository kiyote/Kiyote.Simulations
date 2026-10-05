using System.Numerics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class ThermalRadiation : IThermalRadiation {

	private const Direction Cardinal = Direction.North | Direction.East | Direction.South | Direction.West;

	void IThermalRadiation.Radiate(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		float stefanBoltzmann,
		float spaceTemperature,
		IGridLayer<float> emissivityLayer,
		IGridLayer<float> capacityLayer,
		IGridLayer<float> solarLayer,
		IGridLayer<Direction> vacuumLayer,
		IGridLayer<float> temperatureNextLayer
	) {
		Span<float> emissivity = emissivityLayer.Cells;
		Span<float> capacity = capacityLayer.Cells;
		Span<float> solar = solarLayer.Cells;
		Span<Direction> vacuum = vacuumLayer.Cells;
		Span<float> temperature = temperatureNextLayer.Cells;
		float space4 = spaceTemperature * spaceTemperature * spaceTemperature * spaceTemperature;

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = temperatureNextLayer.IndexOf( slot, 0, row );
			for( int column = 0; column < ChunkSize; column++ ) {
				int i = start + column;
				int faces = BitOperations.PopCount( (uint)( vacuum[i] & Cardinal ) );
				if( ( bits & ( 1u << column ) ) == 0 || faces == 0 || capacity[i] <= 0.0f ) {
					continue;
				}
				float t = temperature[i];
				float loss = emissivity[i] * stefanBoltzmann * faces * ( ( t * t * t * t ) - space4 );
				float next = t + ( ( solar[i] - loss ) * dt / capacity[i] );
				// Radiation alone can never cool a cell below the space it radiates to.
				if( loss > 0.0f && next < spaceTemperature ) {
					next = MathF.Min( t, spaceTemperature );
				}
				temperature[i] = next;
			}
		}
	}

}
