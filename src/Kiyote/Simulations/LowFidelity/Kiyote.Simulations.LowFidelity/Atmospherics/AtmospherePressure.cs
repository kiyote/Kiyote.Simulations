using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmospherePressure : IAtmospherePressure {

	void IAtmospherePressure.Accumulate(
		ReadOnlySpan<ulong> presence,
		ReadOnlySpan<float> gas,
		Span<float> total
	) {
		ref float gasRef = ref MemoryMarshal.GetReference( gas );
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( presence, row );
			if( bits == 0 ) {
				continue;
			}
			int start = ChunkIndex( 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Store( Load( ref totalRef, i ) + Load( ref gasRef, i ), ref totalRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				total[i] += gas[i];
			}
		}
	}

	void IAtmospherePressure.Compute(
		ReadOnlySpan<ulong> presence,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> temperature,
		Span<float> pressure
	) {
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float temperatureRef = ref MemoryMarshal.GetReference( temperature );
		ref float pressureRef = ref MemoryMarshal.GetReference( pressure );
		Vector256<float> reference = Vector256.Create( ReferenceTemperature );
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( presence, row );
			if( bits == 0 ) {
				continue;
			}
			int start = ChunkIndex( 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Store( Load( ref totalRef, i ) * Load( ref temperatureRef, i ) / reference, ref pressureRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				pressure[i] = total[i] * temperature[i] / ReferenceTemperature;
			}
		}
	}

}
