using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereTransport : IAtmosphereTransport {

	void IAtmosphereTransport.Transport(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		IGridLayer<float>[] gas,
		IGridLayer<float>[] gasNext,
		IGridLayer<float> totalLayer,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> ventLayer,
		Span<float> outgoingByCell,
		Span<float> newTotals
	) {
		int stride = flowEastLayer.Stride;
		Span<float> total = totalLayer.Cells;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> vent = ventLayer.Cells;
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float outgoingRef = ref MemoryMarshal.GetReference( outgoingByCell );
		ref float newTotalRef = ref MemoryMarshal.GetReference( newTotals );
		Vector256<float> dtV = Vector256.Create( dt );
		Vector256<float> zero = Vector256<float>.Zero;

		// Signed amount leaving the cell through each face is flow * dt; negative arrives from the neighbour.
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = totalLayer.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int c = 0; c < ChunkSize; c += Lanes ) {
					int i = start + c;
					int local = ( row * ChunkSize ) + c;
					Vector256<float> outgoing = Load( ref ventRef, i ) * dtV;
					outgoing += Vector256.Max( Load( ref eastRef, i ) * dtV, zero );
					outgoing += Vector256.Max( Load( ref southRef, i ) * dtV, zero );
					outgoing += Vector256.Max( -Load( ref eastRef, i - 1 ) * dtV, zero );
					outgoing += Vector256.Max( -Load( ref southRef, i - stride ) * dtV, zero );
					Store( outgoing, ref outgoingRef, local );
					Store( zero, ref newTotalRef, local );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int local = ( row * ChunkSize ) + column;
				float outgoing = vent[i] * dt;
				outgoing += MathF.Max( flowEast[i] * dt, 0.0f );
				outgoing += MathF.Max( flowSouth[i] * dt, 0.0f );
				outgoing += MathF.Max( -flowEast[i - 1] * dt, 0.0f );
				outgoing += MathF.Max( -flowSouth[i - stride] * dt, 0.0f );
				outgoingByCell[local] = outgoing;
				newTotals[local] = 0.0f;
			}
		}

		for( int g = 0; g < gas.Length; g++ ) {
			ReadOnlySpan<float> amounts = gas[g].Cells;
			Span<float> nextAmounts = gasNext[g].Cells;
			ref float amountRef = ref MemoryMarshal.GetReference( amounts );
			ref float nextRef = ref MemoryMarshal.GetReference( nextAmounts );
			for( int row = 0; row < ChunkSize; row++ ) {
				uint bits = GetRowMask( mask, row );
				if( bits == 0 ) {
					continue;
				}
				int start = totalLayer.IndexOf( slot, 0, row );
				if( bits == FullRow ) {
					for( int c = 0; c < ChunkSize; c += Lanes ) {
						int i = start + c;
						int local = ( row * ChunkSize ) + c;
						Vector256<float> own = Load( ref totalRef, i );
						Vector256<float> amount = Load( ref amountRef, i );
						Vector256<float> next = Vector256.ConditionalSelect(
							Vector256.GreaterThan( own, zero ),
							amount - ( Load( ref outgoingRef, local ) * amount / own ),
							amount
						);
						next = Inflow( next, Load( ref eastRef, i ) * dtV, Load( ref amountRef, i + 1 ), Load( ref totalRef, i + 1 ) );
						next = Inflow( next, Load( ref southRef, i ) * dtV, Load( ref amountRef, i + stride ), Load( ref totalRef, i + stride ) );
						next = Inflow( next, -Load( ref eastRef, i - 1 ) * dtV, Load( ref amountRef, i - 1 ), Load( ref totalRef, i - 1 ) );
						next = Inflow( next, -Load( ref southRef, i - stride ) * dtV, Load( ref amountRef, i - stride ), Load( ref totalRef, i - stride ) );
						next = Vector256.Max( next, zero );
						Store( next, ref nextRef, i );
						Store( Load( ref newTotalRef, local ) + next, ref newTotalRef, local );
					}
					continue;
				}
				while( bits != 0 ) {
					int column = BitOperations.TrailingZeroCount( bits );
					bits &= bits - 1;
					int i = start + column;
					int local = ( row * ChunkSize ) + column;
					float own = total[i];
					float amount = amounts[i];
					float next = amount;
					if( own > 0.0f ) {
						next -= outgoingByCell[local] * amount / own;
					}
					next = Inflow( next, flowEast[i] * dt, i + 1, amounts, total );
					next = Inflow( next, flowSouth[i] * dt, i + stride, amounts, total );
					next = Inflow( next, -flowEast[i - 1] * dt, i - 1, amounts, total );
					next = Inflow( next, -flowSouth[i - stride] * dt, i - stride, amounts, total );
					next = MathF.Max( next, 0.0f );
					nextAmounts[i] = next;
					newTotals[local] += next;
				}
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
