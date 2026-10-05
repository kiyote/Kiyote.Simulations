using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmospherePressure : IAtmospherePressure {

	void IAtmospherePressure.Compute(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float>[] gas,
		IGridLayer<float> temperature,
		IGridLayer<float> total,
		IGridLayer<float> pressure
	) {
		SumGas( slot, mask, gas, total );
		Span<float> totals = total.Cells;
		Span<float> pressures = pressure.Cells;
		Span<float> temperatures = temperature.Cells;
		ref float totalRef = ref MemoryMarshal.GetReference( totals );
		ref float pressureRef = ref MemoryMarshal.GetReference( pressures );
		ref float temperatureRef = ref MemoryMarshal.GetReference( temperatures );
		Vector256<float> reference = Vector256.Create( ReferenceTemperature );
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = temperature.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				for( int i = start; i < start + ChunkSize; i += Lanes ) {
					Store( Load( ref totalRef, i ) * Load( ref temperatureRef, i ) / reference, ref pressureRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int i = start + BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				pressures[i] = totals[i] * temperatures[i] / ReferenceTemperature;
			}
		}
	}

	void IAtmospherePressure.SumGas(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float>[] gas,
		IGridLayer<float> target
	) {
		SumGas( slot, mask, gas, target );
	}

	// Writes the sum of all gas layers into target for every valid cell of the slot.
	private static void SumGas(
		int slot,
		ReadOnlySpan<ulong> mask,
		IGridLayer<float>[] gas,
		IGridLayer<float> target
	) {
		Span<float> targets = target.Cells;
		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = target.IndexOf( slot, 0, row );
			if( bits == FullRow ) {
				targets.Slice( start, ChunkSize ).Clear();
				continue;
			}
			while( bits != 0 ) {
				targets[start + BitOperations.TrailingZeroCount( bits )] = 0.0f;
				bits &= bits - 1;
			}
		}
		ref float targetRef = ref MemoryMarshal.GetReference( targets );
		for( int g = 0; g < gas.Length; g++ ) {
			ReadOnlySpan<float> amounts = gas[g].Cells;
			ref float gasRef = ref MemoryMarshal.GetReference( amounts );
			for( int row = 0; row < ChunkSize; row++ ) {
				uint bits = GetRowMask( mask, row );
				if( bits == 0 ) {
					continue;
				}
				int start = target.IndexOf( slot, 0, row );
				if( bits == FullRow ) {
					for( int i = start; i < start + ChunkSize; i += Lanes ) {
						Store( Load( ref targetRef, i ) + Load( ref gasRef, i ), ref targetRef, i );
					}
					continue;
				}
				while( bits != 0 ) {
					int i = start + BitOperations.TrailingZeroCount( bits );
					bits &= bits - 1;
					targets[i] += amounts[i];
				}
			}
		}
	}

}
