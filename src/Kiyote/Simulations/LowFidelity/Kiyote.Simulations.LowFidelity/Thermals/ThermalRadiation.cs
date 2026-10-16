using System.Numerics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class ThermalRadiation : IThermalRadiation {

	private const Direction Cardinal = Direction.North | Direction.East | Direction.South | Direction.West;

	void IThermalRadiation.Radiate(
		ReadOnlySpan<ulong> presence,
		float dt,
		float stefanBoltzmann,
		float spaceTemperature,
		ReadOnlySpan<float> emissivity,
		ReadOnlySpan<float> capacity,
		ReadOnlySpan<float> solar,
		ReadOnlySpan<Direction> vacuum,
		Span<float> temperatureNext
	) {
		float space4 = spaceTemperature * spaceTemperature * spaceTemperature * spaceTemperature;

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( presence, row );
			int start = ChunkIndex( 0, row );
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int faces = BitOperations.PopCount( (uint)( vacuum[i] & Cardinal ) );
				if( faces == 0 || capacity[i] <= 0.0f ) {
					continue;
				}
				float t = temperatureNext[i];
				float loss = emissivity[i] * stefanBoltzmann * faces * ( ( t * t * t * t ) - space4 );
				float next = t + ( ( solar[i] - loss ) * dt / capacity[i] );
				// Radiation alone can never cool a cell below the space it radiates to.
				if( loss > 0.0f && next < spaceTemperature ) {
					next = MathF.Min( t, spaceTemperature );
				}
				temperatureNext[i] = next;
			}
		}
	}

}
