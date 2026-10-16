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
		ReadOnlySpan<ulong> presence,
		float k,
		float damp,
		bool eastOpen,
		bool southOpen,
		ReadOnlySpan<float> pressure,
		Span<float> flowEast,
		Span<float> flowSouth,
		Span<float> vent,
		ReadOnlySpan<Direction> connectivity,
		ReadOnlySpan<Direction> vacuum,
		ReadOnlySpan<bool> permeable
	) {
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
			uint bits = GetRowMask( presence, row );
			if( bits == 0 ) {
				continue;
			}
			int start = ChunkIndex( 0, row );
			int paddedStart = PaddedIndex( 0, row );
			bool southAllowed = row < ChunkSize - 1 || southOpen;
			if( bits == FullRow ) {
				for( int c = 0; c < ChunkSize; c += Lanes ) {
					int i = start + c;
					int p = paddedStart + c;
					Vector256<float> self = Load( ref pressureRef, p );
					Vector256<int> open = LoadFlags( ref openRef, i );
					Vector256<float> east = HasFlag( open, Direction.East );
					if( c + Lanes == ChunkSize ) {
						east &= lastBlockEast;
					}
					Vector256<float> south = southAllowed ? HasFlag( open, Direction.South ) : Vector256<float>.Zero;
					Store( Vector256.ConditionalSelect( east, ( Load( ref eastRef, i ) + ( kV * ( self - Load( ref pressureRef, p + 1 ) ) ) ) * dampV, Vector256<float>.Zero ), ref eastRef, i );
					Store( Vector256.ConditionalSelect( south, ( Load( ref southRef, i ) + ( kV * ( self - Load( ref pressureRef, p + PaddedStride ) ) ) ) * dampV, Vector256<float>.Zero ), ref southRef, i );

					// Each open cardinal vacuum face yields an all-bits-set lane (-1); negate the sum to count them.
					Vector256<int> vac = LoadFlags( ref vacuumRef, i );
					Vector256<int> faceCount = -( HasFlag( vac, Direction.North ).AsInt32()
						+ HasFlag( vac, Direction.East ).AsInt32()
						+ HasFlag( vac, Direction.South ).AsInt32()
						+ HasFlag( vac, Direction.West ).AsInt32() );
					Vector256<float> faces = Vector256.ConvertToSingle( faceCount ) & IsSet( ref permeableRef, i );
					Store( Vector256.ConditionalSelect(
						Vector256.GreaterThan( faces, Vector256<float>.Zero ),
						Vector256.Max( Vector256<float>.Zero, ( Load( ref ventRef, i ) + ( kV * self * faces ) ) * dampV ),
						Vector256<float>.Zero
					), ref ventRef, i );
				}
				continue;
			}
			while( bits != 0 ) {
				int column = BitOperations.TrailingZeroCount( bits );
				bits &= bits - 1;
				int i = start + column;
				int p = paddedStart + column;
				Direction open = connectivity[i];

				flowEast[i] = ( open & Direction.East ) != 0 && ( column < ChunkSize - 1 || eastOpen )
					? ( flowEast[i] + ( k * ( pressure[p] - pressure[p + 1] ) ) ) * damp
					: 0.0f;
				flowSouth[i] = ( open & Direction.South ) != 0 && southAllowed
					? ( flowSouth[i] + ( k * ( pressure[p] - pressure[p + PaddedStride] ) ) ) * damp
					: 0.0f;

				int faces = permeable[i] ? BitOperations.PopCount( (uint)( vacuum[i] & Cardinal ) ) : 0;
				vent[i] = faces > 0
					? MathF.Max( 0.0f, ( vent[i] + ( k * pressure[p] * faces ) ) * damp )
					: 0.0f;
			}
		}
	}

}
