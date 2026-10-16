using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereTransport : IAtmosphereTransport {

	// Signed amount leaving the cell through each face is flow * dt; negative arrives from the neighbour.
	void IAtmosphereTransport.Prepare(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> vent,
		Span<float> outgoing,
		Span<float> newTotal
	) {
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float outgoingRef = ref MemoryMarshal.GetReference( outgoing );
		ref float newTotalRef = ref MemoryMarshal.GetReference( newTotal );
		Vector256<float> dtV = Vector256.Create( dt );
		Vector256<float> zero = Vector256<float>.Zero;

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
					Vector256<float> leaving = Load( ref ventRef, i ) * dtV;
					leaving += Vector256.Max( Load( ref eastRef, p ) * dtV, zero );
					leaving += Vector256.Max( Load( ref southRef, p ) * dtV, zero );
					leaving += Vector256.Max( -Load( ref eastRef, p - 1 ) * dtV, zero );
					leaving += Vector256.Max( -Load( ref southRef, p - PaddedStride ) * dtV, zero );
					Store( leaving, ref outgoingRef, i );
					Store( zero, ref newTotalRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				float leaving = vent[i] * dt;
				leaving += MathF.Max( flowEast[p] * dt, 0.0f );
				leaving += MathF.Max( flowSouth[p] * dt, 0.0f );
				leaving += MathF.Max( -flowEast[p - 1] * dt, 0.0f );
				leaving += MathF.Max( -flowSouth[p - PaddedStride] * dt, 0.0f );
				outgoing[i] = leaving;
				newTotal[i] = 0.0f;
			}
		}
	}

	void IAtmosphereTransport.Transport(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> gas,
		ReadOnlySpan<float> total,
		ReadOnlySpan<float> flowEast,
		ReadOnlySpan<float> flowSouth,
		ReadOnlySpan<float> outgoing,
		Span<float> gasNext,
		Span<float> newTotal
	) {
		ref float amountRef = ref MemoryMarshal.GetReference( gas );
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float outgoingRef = ref MemoryMarshal.GetReference( outgoing );
		ref float nextRef = ref MemoryMarshal.GetReference( gasNext );
		ref float newTotalRef = ref MemoryMarshal.GetReference( newTotal );
		Vector256<float> dtV = Vector256.Create( dt );
		Vector256<float> zero = Vector256<float>.Zero;

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
					Vector256<float> own = Load( ref totalRef, p );
					Vector256<float> amount = Load( ref amountRef, p );
					Vector256<float> next = Vector256.ConditionalSelect(
						Vector256.GreaterThan( own, zero ),
						amount - ( Load( ref outgoingRef, i ) * amount / own ),
						amount
					);
					next = Inflow( next, Load( ref eastRef, p ) * dtV, Load( ref amountRef, p + 1 ), Load( ref totalRef, p + 1 ) );
					next = Inflow( next, Load( ref southRef, p ) * dtV, Load( ref amountRef, p + PaddedStride ), Load( ref totalRef, p + PaddedStride ) );
					next = Inflow( next, -Load( ref eastRef, p - 1 ) * dtV, Load( ref amountRef, p - 1 ), Load( ref totalRef, p - 1 ) );
					next = Inflow( next, -Load( ref southRef, p - PaddedStride ) * dtV, Load( ref amountRef, p - PaddedStride ), Load( ref totalRef, p - PaddedStride ) );
					next = Vector256.Max( next, zero );
					Store( next, ref nextRef, i );
					Store( Load( ref newTotalRef, i ) + next, ref newTotalRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				float own = total[p];
				float amount = gas[p];
				float next = amount;
				if( own > 0.0f ) {
					next -= outgoing[i] * amount / own;
				}
				next = Inflow( next, flowEast[p] * dt, p + 1, gas, total );
				next = Inflow( next, flowSouth[p] * dt, p + PaddedStride, gas, total );
				next = Inflow( next, -flowEast[p - 1] * dt, p - 1, gas, total );
				next = Inflow( next, -flowSouth[p - PaddedStride] * dt, p - PaddedStride, gas, total );
				next = MathF.Max( next, 0.0f );
				gasNext[i] = next;
				newTotal[i] += next;
			}
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static Vector256<float> Inflow(
		Vector256<float> value,
		Vector256<float> move,
		Vector256<float> amount,
		Vector256<float> total
	) {
		Vector256<float> take = Vector256.LessThan( move, Vector256<float>.Zero ) & Vector256.GreaterThan( total, Vector256<float>.Zero );
		return Vector256.ConditionalSelect( take, value - ( move * amount / total ), value );
	}

	private static float Inflow(
		float value,
		float move,
		int donor,
		ReadOnlySpan<float> amounts,
		ReadOnlySpan<float> total
	) {
		return move < 0.0f && total[donor] > 0.0f
			? value - ( move * amounts[donor] / total[donor] )
			: value;
	}

}
