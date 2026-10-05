using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereWind : IAtmosphereWind {

	void IAtmosphereWind.Publish(
		int slot,
		ReadOnlySpan<ulong> mask,
		float windScale,
		IGridLayer<float> temperatureLayer,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> pressureLayer,
		IGridLayer<float> windXLayer,
		IGridLayer<float> windYLayer
	) {
		int stride = flowEastLayer.Stride;
		Span<float> pressure = pressureLayer.Cells;
		Span<float> temperature = temperatureLayer.Cells;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> windX = windXLayer.Cells;
		Span<float> windY = windYLayer.Cells;
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
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = temperatureLayer.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Vector256<float> total = Load( ref pressureRef, i );
					Store( total * Load( ref temperatureRef, i ) / reference, ref pressureRef, i );
					Vector256<float> hasGas = Vector256.GreaterThan( total, Vector256<float>.Zero );
					Vector256<float> vx = ( Load( ref eastRef, i - 1 ) + Load( ref eastRef, i ) ) * half / total;
					Vector256<float> vy = ( Load( ref southRef, i - stride ) + Load( ref southRef, i ) ) * half / total;
					Vector256<float> speed = Vector256.Sqrt( ( vx * vx ) + ( vy * vy ) );
					Vector256<float> dynamic = windScaleV * total * speed;
					Store( Vector256.ConditionalSelect( hasGas, dynamic * vx, Vector256<float>.Zero ), ref windXRef, i );
					Store( Vector256.ConditionalSelect( hasGas, dynamic * vy, Vector256<float>.Zero ), ref windYRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				float total = pressure[i];
				pressure[i] = total * temperature[i] / ReferenceTemperature;
				if( total <= 0.0f ) {
					windX[i] = 0.0f;
					windY[i] = 0.0f;
					continue;
				}
				float vx = ( flowEast[i - 1] + flowEast[i] ) * 0.5f / total;
				float vy = ( flowSouth[i - stride] + flowSouth[i] ) * 0.5f / total;
				float speed = MathF.Sqrt( ( vx * vx ) + ( vy * vy ) );
				float dynamic = windScale * total * speed;
				windX[i] = dynamic * vx;
				windY[i] = dynamic * vy;
			}
		}
	}

}
