using System.Numerics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereCondensation : IAtmosphereCondensation {

	bool IAtmosphereCondensation.Condense(
		int slot,
		ReadOnlySpan<ulong> mask,
		float condensation,
		IGasRegistry gases,
		IGridLayer<float> temperatureNextLayer,
		IGridLayer<float>[] gasNextLayers,
		IGridLayer<float>?[] condensateLayers
	) {
		bool changing = false;
		Span<float> temperatureNext = temperatureNextLayer.Cells;
		for( int g = 0; g < gasNextLayers.Length; g++ ) {
			IGridLayer<float>? layer = condensateLayers[g];
			if( layer is null ) {
				continue;
			}
			float point = gases.GetDefinition( new GasIndex( g ) ).CondensationPoint!.Value;
			Span<float> gasNext = gasNextLayers[g].Cells;
			Span<float> condensates = layer.Cells;
			bool dirty = false;
			for( int row = 0; row < ChunkSize; row++ ) {
				uint bits = GetRowMask( mask, row );
				if( bits == 0 ) {
					continue;
				}
				int start = temperatureNextLayer.IndexOf( slot, 0, row );
				while( bits != 0 ) {
					int i = start + BitOperations.TrailingZeroCount( bits );
					bits &= bits - 1;
					float nextTemperature = temperatureNext[i];
					ref float gas = ref gasNext[i];
					ref float condensate = ref condensates[i];
					float phase = 0.0f;
					if( nextTemperature < point ) {
						phase = gas * condensation;
					} else if( nextTemperature > point ) {
						phase = -condensate * condensation;
					}
					if( phase != 0.0f ) {
						gas -= phase;
						condensate += phase;
						dirty = true;
						if( MathF.Abs( phase ) > Epsilon ) {
							changing = true;
						}
					}
				}
			}
			if( dirty ) {
				layer.MarkDirty( slot );
			}
		}
		return changing;
	}

}
