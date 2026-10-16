using System.Numerics;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereCondensation : IAtmosphereCondensation {

	bool IAtmosphereCondensation.Condense(
		ReadOnlySpan<ulong> presence,
		float condensation,
		float condensationPoint,
		ReadOnlySpan<float> temperatureNext,
		Span<float> gasNext,
		Span<float> condensate,
		out bool modified
	) {
		bool changing = false;
		modified = false;
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( presence, row );
			int start = ChunkIndex( 0, row );
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				float nextTemperature = temperatureNext[i];
				float phase = 0.0f;
				if( nextTemperature < condensationPoint ) {
					phase = gasNext[i] * condensation;
				} else if( nextTemperature > condensationPoint ) {
					phase = -condensate[i] * condensation;
				}
				if( phase != 0.0f ) {
					gasNext[i] -= phase;
					condensate[i] += phase;
					modified = true;
					if( MathF.Abs( phase ) > Epsilon ) {
						changing = true;
					}
				}
			}
		}
		return changing;
	}

}
