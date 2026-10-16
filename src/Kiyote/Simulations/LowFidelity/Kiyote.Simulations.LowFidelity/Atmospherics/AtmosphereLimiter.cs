using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereLimiter : IAtmosphereLimiter {

	void IAtmosphereLimiter.ComputeScale(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> vent,
		Span<float> scale
	) {
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float scaleRef = ref MemoryMarshal.GetReference( scale );
		Vector256<float> dtV = Vector256.Create( dt );

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
					Vector256<float> outflow = Vector256.Max( Load( ref eastRef, p ), Vector256<float>.Zero )
						+ Vector256.Max( Load( ref southRef, p ), Vector256<float>.Zero )
						+ Vector256.Max( -Load( ref eastRef, p - 1 ), Vector256<float>.Zero )
						+ Vector256.Max( -Load( ref southRef, p - PaddedStride ), Vector256<float>.Zero )
						+ Load( ref ventRef, i );
					outflow *= dtV;
					Vector256<float> t = Load( ref totalRef, i );
					Store( Vector256.ConditionalSelect( Vector256.GreaterThan( outflow, t ), t / outflow, Vector256<float>.One ), ref scaleRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				float outflow = MathF.Max( flowEast[p], 0.0f )
					+ MathF.Max( flowSouth[p], 0.0f )
					+ MathF.Max( -flowEast[p - 1], 0.0f )
					+ MathF.Max( -flowSouth[p - PaddedStride], 0.0f )
					+ vent[i];
				outflow *= dt;
				scale[i] = outflow > total[i] ? total[i] / outflow : 1.0f;
			}
		}
	}

	void IAtmosphereLimiter.Limit(
		ReadOnlySpan<ulong> presence,
		Span<float> flowEast,
		Span<float> flowSouth,
		Span<float> vent,
		ReadOnlySpan<float> scale
	) {
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float scaleRef = ref MemoryMarshal.GetReference( scale );

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
					Vector256<float> s = Load( ref scaleRef, p );
					Vector256<float> east = Load( ref eastRef, i );
					Vector256<float> south = Load( ref southRef, i );
					Store( east * Vector256.ConditionalSelect( Vector256.GreaterThan( east, Vector256<float>.Zero ), s, Load( ref scaleRef, p + 1 ) ), ref eastRef, i );
					Store( south * Vector256.ConditionalSelect( Vector256.GreaterThan( south, Vector256<float>.Zero ), s, Load( ref scaleRef, p + PaddedStride ) ), ref southRef, i );
					Store( Load( ref ventRef, i ) * s, ref ventRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				flowEast[i] *= flowEast[i] > 0.0f ? scale[p] : scale[p + 1];
				flowSouth[i] *= flowSouth[i] > 0.0f ? scale[p] : scale[p + PaddedStride];
				vent[i] *= scale[p];
			}
		}
	}

}
