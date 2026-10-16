using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereWind : IAtmosphereWind {

	void IAtmosphereWind.Publish(
		ReadOnlySpan<ulong> presence,
		float windScale,
		ReadOnlySpan<float> temperature,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		Span<float> pressure,
		Span<float> windX,
		Span<float> windY
	) {
		ref float pressureRef = ref MemoryMarshal.GetReference( pressure );
		ref float temperatureRef = ref MemoryMarshal.GetReference( temperature );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float windXRef = ref MemoryMarshal.GetReference( windX );
		ref float windYRef = ref MemoryMarshal.GetReference( windY );
		Vector256<float> reference = Vector256.Create( ReferenceTemperature );
		Vector256<float> half = Vector256.Create( 0.5f );
		Vector256<float> windScaleV = Vector256.Create( windScale );

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( presence, row );
			if( bits == 0 ) {
				continue;
			}
			int start = ChunkIndex( 0, row );
			int paddedStart = PaddedIndex( 0, row );
			if( bits == FullRow ) {
				for( int c = 0; c < ChunkSize; c += Lanes ) {
					int i = start + c;
					int p = paddedStart + c;
					Vector256<float> total = Load( ref pressureRef, i );
					Store( total * Load( ref temperatureRef, i ) / reference, ref pressureRef, i );
					Vector256<float> hasGas = Vector256.GreaterThan( total, Vector256<float>.Zero );
					Vector256<float> vx = ( Load( ref eastRef, p - 1 ) + Load( ref eastRef, p ) ) * half / total;
					Vector256<float> vy = ( Load( ref southRef, p - PaddedStride ) + Load( ref southRef, p ) ) * half / total;
					Vector256<float> speed = Vector256.Sqrt( ( vx * vx ) + ( vy * vy ) );
					Vector256<float> dynamic = windScaleV * total * speed;
					Store( Vector256.ConditionalSelect( hasGas, dynamic * vx, Vector256<float>.Zero ), ref windXRef, i );
					Store( Vector256.ConditionalSelect( hasGas, dynamic * vy, Vector256<float>.Zero ), ref windYRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				float total = pressure[i];
				pressure[i] = total * temperature[i] / ReferenceTemperature;
				if( total <= 0.0f ) {
					windX[i] = 0.0f;
					windY[i] = 0.0f;
					continue;
				}
				float vx = ( flowEast[p - 1] + flowEast[p] ) * 0.5f / total;
				float vy = ( flowSouth[p - PaddedStride] + flowSouth[p] ) * 0.5f / total;
				float speed = MathF.Sqrt( ( vx * vx ) + ( vy * vy ) );
				float dynamic = windScale * total * speed;
				windX[i] = dynamic * vx;
				windY[i] = dynamic * vy;
			}
		}
	}

}
