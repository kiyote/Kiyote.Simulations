using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereLimiter : IAtmosphereLimiter {

	void IAtmosphereLimiter.ComputeScale(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float> totalLayer,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> ventLayer,
		IGridLayer<float> scaleLayer
	) {
		int stride = flowEastLayer.Stride;
		Span<float> total = totalLayer.Cells;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> vent = ventLayer.Cells;
		Span<float> scale = scaleLayer.Cells;
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float scaleRef = ref MemoryMarshal.GetReference( scale );
		Vector256<float> dtV = Vector256.Create( dt );

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = scaleLayer.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Vector256<float> outflow = Vector256.Max( Load( ref eastRef, i ), Vector256<float>.Zero )
						+ Vector256.Max( Load( ref southRef, i ), Vector256<float>.Zero )
						+ Vector256.Max( -Load( ref eastRef, i - 1 ), Vector256<float>.Zero )
						+ Vector256.Max( -Load( ref southRef, i - stride ), Vector256<float>.Zero )
						+ Load( ref ventRef, i );
					outflow *= dtV;
					Vector256<float> t = Load( ref totalRef, i );
					Store( Vector256.ConditionalSelect( Vector256.GreaterThan( outflow, t ), t / outflow, Vector256<float>.One ), ref scaleRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				float outflow = MathF.Max( flowEast[i], 0.0f )
					+ MathF.Max( flowSouth[i], 0.0f )
					+ MathF.Max( -flowEast[i - 1], 0.0f )
					+ MathF.Max( -flowSouth[i - stride], 0.0f )
					+ vent[i];
				outflow *= dt;
				scale[i] = outflow > total[i] ? total[i] / outflow : 1.0f;
			}
		}
	}

	void IAtmosphereLimiter.Limit(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> ventLayer,
		IGridLayer<float> scaleLayer
	) {
		int stride = flowEastLayer.Stride;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> vent = ventLayer.Cells;
		Span<float> scale = scaleLayer.Cells;
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float scaleRef = ref MemoryMarshal.GetReference( scale );

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = scaleLayer.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Vector256<float> s = Load( ref scaleRef, i );
					Vector256<float> east = Load( ref eastRef, i );
					Vector256<float> south = Load( ref southRef, i );
					Store( east * Vector256.ConditionalSelect( Vector256.GreaterThan( east, Vector256<float>.Zero ), s, Load( ref scaleRef, i + 1 ) ), ref eastRef, i );
					Store( south * Vector256.ConditionalSelect( Vector256.GreaterThan( south, Vector256<float>.Zero ), s, Load( ref scaleRef, i + stride ) ), ref southRef, i );
					Store( Load( ref ventRef, i ) * s, ref ventRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				flowEast[i] *= flowEast[i] > 0.0f ? scale[i] : scale[i + 1];
				flowSouth[i] *= flowSouth[i] > 0.0f ? scale[i] : scale[i + stride];
				vent[i] *= scale[i];
			}
		}
	}

}
