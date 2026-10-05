using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereThermal : IAtmosphereThermal {

	bool IAtmosphereThermal.Mix(
		int slot,
		ReadOnlySpan<ulong> mask,
		float dt,
		float conduction,
		IGridLayer<float> totalLayer,
		IGridLayer<float> temperatureLayer,
		IGridLayer<float> temperatureNextLayer,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> ventLayer,
		IGridLayer<Direction> connectivityLayer,
		ReadOnlySpan<float> outgoingByCell,
		ReadOnlySpan<float> newTotals,
		ref float vented
	) {
		int stride = flowEastLayer.Stride;
		Span<float> total = totalLayer.Cells;
		Span<float> temperature = temperatureLayer.Cells;
		Span<float> temperatureNext = temperatureNextLayer.Cells;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> vent = ventLayer.Cells;
		Span<Direction> connectivity = connectivityLayer.Cells;
		bool changing = false;
		ref float totalRef = ref MemoryMarshal.GetReference( total );
		ref float temperatureRef = ref MemoryMarshal.GetReference( temperature );
		ref float temperatureNextRef = ref MemoryMarshal.GetReference( temperatureNext );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref float outgoingRef = ref MemoryMarshal.GetReference( outgoingByCell );
		ref float newTotalRef = ref MemoryMarshal.GetReference( newTotals );
		ref byte openRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( connectivity ) );
		Vector256<float> dtV = Vector256.Create( dt );
		Vector256<float> zero = Vector256<float>.Zero;

		float ventedSum = vented;
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = temperatureLayer.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i++ ) {
					ventedSum += vent[i] * dt;
				}
				for( int c = 0; c < ChunkSize; c += Lanes ) {
					int i = start + c;
					int local = ( row * ChunkSize ) + c;
					Vector256<float> own = Load( ref totalRef, i );
					Vector256<float> newTotal = Load( ref newTotalRef, local );
					Vector256<float> t = Load( ref temperatureRef, i );
					Vector256<float> east = Load( ref eastRef, i );
					Vector256<float> south = Load( ref southRef, i );
					Vector256<float> incoming = zero;
					Vector256<float> energy = zero;
					Arrive( east * dtV, Load( ref temperatureRef, i + 1 ), ref incoming, ref energy );
					Arrive( south * dtV, Load( ref temperatureRef, i + stride ), ref incoming, ref energy );
					Arrive( -Load( ref eastRef, i - 1 ) * dtV, Load( ref temperatureRef, i - 1 ), ref incoming, ref energy );
					Arrive( -Load( ref southRef, i - stride ) * dtV, Load( ref temperatureRef, i - stride ), ref incoming, ref energy );

					Vector256<float> remaining = Vector256.Max( own - Load( ref outgoingRef, local ), zero );
					Vector256<float> mixed = remaining + incoming;
					Vector256<float> hasGas = Vector256.GreaterThan( newTotal, zero );
					Vector256<float> nextTemperature = Vector256.ConditionalSelect(
						hasGas & Vector256.GreaterThan( mixed, zero ),
						( ( remaining * t ) + energy ) / mixed,
						t
					);
					if( conduction > 0.0f ) {
						Vector256<int> open = LoadFlags( ref openRef, i );
						Vector256<float> exchange = zero;
						exchange += Vector256.ConditionalSelect( HasFlag( open, Direction.East ), Load( ref temperatureRef, i + 1 ) - t, zero );
						exchange += Vector256.ConditionalSelect( HasFlag( open, Direction.South ), Load( ref temperatureRef, i + stride ) - t, zero );
						exchange += Vector256.ConditionalSelect( HasFlag( open, Direction.West ), Load( ref temperatureRef, i - 1 ) - t, zero );
						exchange += Vector256.ConditionalSelect( HasFlag( open, Direction.North ), Load( ref temperatureRef, i - stride ) - t, zero );
						nextTemperature = Vector256.ConditionalSelect( hasGas, nextTemperature + ( Vector256.Create( conduction ) * exchange ), nextTemperature );
					}
					Store( nextTemperature, ref temperatureNextRef, i );

					Vector256<float> epsilon = Vector256.Create( Epsilon );
					Vector256<float> moving = Vector256.GreaterThan( Vector256.Abs( newTotal - own ), epsilon )
						| Vector256.GreaterThan( Vector256.Abs( nextTemperature - t ), epsilon )
						| Vector256.GreaterThan( Vector256.Abs( east ), epsilon )
						| Vector256.GreaterThan( Vector256.Abs( south ), epsilon )
						| Vector256.GreaterThan( Load( ref ventRef, i ), epsilon );
					if( moving.ExtractMostSignificantBits() != 0 ) {
						changing = true;
					}
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int local = ( row * ChunkSize ) + column;
				float cellVented = vent[i] * dt;
				float own = total[i];
				float outgoing = outgoingByCell[local];
				float newTotal = newTotals[local];
				float energy = 0.0f;
				float incoming = 0.0f;
				Arrive( flowEast[i] * dt, temperature[i + 1], ref incoming, ref energy );
				Arrive( flowSouth[i] * dt, temperature[i + stride], ref incoming, ref energy );
				Arrive( -flowEast[i - 1] * dt, temperature[i - 1], ref incoming, ref energy );
				Arrive( -flowSouth[i - stride] * dt, temperature[i - stride], ref incoming, ref energy );

				float t = temperature[i];
				float remaining = MathF.Max( own - outgoing, 0.0f );
				float mixed = remaining + incoming;
				float nextTemperature = newTotal > 0.0f && mixed > 0.0f
					? ( ( remaining * t ) + energy ) / mixed
					: t;
				if( newTotal > 0.0f && conduction > 0.0f ) {
					Direction open = connectivity[i];
					float exchange = 0.0f;
					if( ( open & Direction.East ) != 0 ) {
						exchange += temperature[i + 1] - t;
					}
					if( ( open & Direction.South ) != 0 ) {
						exchange += temperature[i + stride] - t;
					}
					if( ( open & Direction.West ) != 0 ) {
						exchange += temperature[i - 1] - t;
					}
					if( ( open & Direction.North ) != 0 ) {
						exchange += temperature[i - stride] - t;
					}
					nextTemperature += conduction * exchange;
				}
				temperatureNext[i] = nextTemperature;

				ventedSum += cellVented;
				if( MathF.Abs( newTotal - own ) > Epsilon
					|| MathF.Abs( nextTemperature - t ) > Epsilon
					|| MathF.Abs( flowEast[i] ) > Epsilon
					|| MathF.Abs( flowSouth[i] ) > Epsilon
					|| vent[i] > Epsilon
				) {
					changing = true;
				}
			}
		}
		vented = ventedSum;
		return changing;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static void Arrive(
		Vector256<float> move,
		Vector256<float> donorTemperature,
		ref Vector256<float> incoming,
		ref Vector256<float> energy
	) {
		Vector256<float> arriving = Vector256.LessThan( move, Vector256<float>.Zero );
		incoming = Vector256.ConditionalSelect( arriving, incoming - move, incoming );
		energy = Vector256.ConditionalSelect( arriving, energy - ( move * donorTemperature ), energy );
	}

	private static void Arrive(
		float move,
		float donorTemperature,
		ref float incoming,
		ref float energy
	) {
		if( move < 0.0f ) {
			incoming -= move;
			energy -= move * donorTemperature;
		}
	}

}
