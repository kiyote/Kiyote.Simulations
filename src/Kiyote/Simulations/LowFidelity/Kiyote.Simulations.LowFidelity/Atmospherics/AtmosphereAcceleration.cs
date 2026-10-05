using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereAcceleration : IAtmosphereAcceleration {

	private const Direction Cardinal = Direction.North | Direction.East | Direction.South | Direction.West;

	void IAtmosphereAcceleration.Accelerate(
		int slot,
		ReadOnlySpan<ulong> mask,
		float k,
		float damp,
		bool eastOpen,
		bool southOpen,
		IGridLayer<float> pressureLayer,
		IGridLayer<float> flowEastLayer,
		IGridLayer<float> flowSouthLayer,
		IGridLayer<float> ventLayer,
		IGridLayer<Direction> connectivityLayer,
		IGridLayer<Direction> vacuumLayer,
		IGridLayer<bool> permeableLayer
	) {
		int stride = pressureLayer.Stride;
		Span<float> pressure = pressureLayer.Cells;
		Span<float> flowEast = flowEastLayer.Cells;
		Span<float> flowSouth = flowSouthLayer.Cells;
		Span<float> vent = ventLayer.Cells;
		Span<Direction> connectivity = connectivityLayer.Cells;
		Span<Direction> vacuum = vacuumLayer.Cells;
		Span<bool> permeable = permeableLayer.Cells;
		ref float pressureRef = ref MemoryMarshal.GetReference( pressure );
		ref float eastRef = ref MemoryMarshal.GetReference( flowEast );
		ref float southRef = ref MemoryMarshal.GetReference( flowSouth );
		ref float ventRef = ref MemoryMarshal.GetReference( vent );
		ref byte openRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( connectivity ) );
		ref byte vacuumRef = ref Unsafe.As<Direction, byte>( ref MemoryMarshal.GetReference( vacuum ) );
		ref byte permeableRef = ref Unsafe.As<bool, byte>( ref MemoryMarshal.GetReference( permeable ) );
		Vector256<float> kV = Vector256.Create( k );
		Vector256<float> dampV = Vector256.Create( damp );
		// The last column may only flow east when the eastern chunk is being processed.
		Vector256<float> lastBlockEast = eastOpen
			? Vector256<float>.AllBitsSet
			: Vector256.Create( -1, -1, -1, -1, -1, -1, -1, 0 ).AsSingle();

		for( int row = 0; row < ChunkSize; row++ ) {
			uint bits = GetRowMask( mask, row );
			if( bits == 0 ) {
				continue;
			}
			int start = pressureLayer.IndexOf( slot, 0, row );
			bool southAllowed = row < ChunkSize - 1 || southOpen;
			if( bits == FullRow ) {
				for( int c = 0; c < ChunkSize; c += Lanes ) {
					int i = start + c;
					Vector256<float> p = Load( ref pressureRef, i );
					Vector256<int> open = LoadFlags( ref openRef, i );
					Vector256<float> east = HasFlag( open, Direction.East );
					if( c + Lanes == ChunkSize ) {
						east &= lastBlockEast;
					}
					Vector256<float> south = southAllowed ? HasFlag( open, Direction.South ) : Vector256<float>.Zero;
					Store( Vector256.ConditionalSelect( east, ( Load( ref eastRef, i ) + ( kV * ( p - Load( ref pressureRef, i + 1 ) ) ) ) * dampV, Vector256<float>.Zero ), ref eastRef, i );
					Store( Vector256.ConditionalSelect( south, ( Load( ref southRef, i ) + ( kV * ( p - Load( ref pressureRef, i + stride ) ) ) ) * dampV, Vector256<float>.Zero ), ref southRef, i );

					// Each open cardinal vacuum face yields an all-bits-set lane (-1); negate the sum to count them.
					Vector256<int> vac = LoadFlags( ref vacuumRef, i );
					Vector256<int> faceCount = -( HasFlag( vac, Direction.North ).AsInt32()
						+ HasFlag( vac, Direction.East ).AsInt32()
						+ HasFlag( vac, Direction.South ).AsInt32()
						+ HasFlag( vac, Direction.West ).AsInt32() );
					Vector256<float> faces = Vector256.ConvertToSingle( faceCount ) & IsSet( ref permeableRef, i );
					Store( Vector256.ConditionalSelect(
						Vector256.GreaterThan( faces, Vector256<float>.Zero ),
						Vector256.Max( Vector256<float>.Zero, ( Load( ref ventRef, i ) + ( kV * p * faces ) ) * dampV ),
						Vector256<float>.Zero
					), ref ventRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				Direction open = connectivity[i];

				flowEast[i] = ( open & Direction.East ) != 0 && ( column < ChunkSize - 1 || eastOpen )
					? ( flowEast[i] + ( k * ( pressure[i] - pressure[i + 1] ) ) ) * damp
					: 0.0f;
				flowSouth[i] = ( open & Direction.South ) != 0 && southAllowed
					? ( flowSouth[i] + ( k * ( pressure[i] - pressure[i + stride] ) ) ) * damp
					: 0.0f;

				int faces = permeable[i] ? BitOperations.PopCount( (uint)( vacuum[i] & Cardinal ) ) : 0;
				vent[i] = faces > 0
					? MathF.Max( 0.0f, ( vent[i] + ( k * pressure[i] * faces ) ) * damp )
					: 0.0f;
			}
		}
	}

}
