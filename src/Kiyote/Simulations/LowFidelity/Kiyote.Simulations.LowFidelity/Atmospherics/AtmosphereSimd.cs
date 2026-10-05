using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Shared constants and SIMD helpers for the atmosphere stages.
internal static class AtmosphereSimd {

	public const int ChunkSize = 16;
	public const int Lanes = 8;
	public const uint FullRow = ( 1u << ChunkSize ) - 1;
	public const float ReferenceTemperature = 293.15f;
	public const float Epsilon = 1e-4f;

	// Validity bits for one chunk row; bit n is column n.
	public static uint GetRowMask(
		ReadOnlySpan<ulong> mask,
		int row
	) {
		int bit = row * ChunkSize;
		return (uint)( ( mask[bit >> 6] >> ( bit & 63 ) ) & ( ( 1UL << ChunkSize ) - 1 ) );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> Load(
		ref float source,
		int index
	) {
		return Vector256.LoadUnsafe( ref source, (nuint)index );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void Store(
		Vector256<float> value,
		ref float destination,
		int index
	) {
		value.StoreUnsafe( ref destination, (nuint)index );
	}

	// Widens 8 consecutive byte flags into one int lane each.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<int> LoadFlags(
		ref byte flags,
		int index
	) {
		Vector128<byte> packed = Vector128.CreateScalarUnsafe( Unsafe.ReadUnaligned<ulong>( ref Unsafe.Add( ref flags, index ) ) ).AsByte();
		(Vector128<uint> low, Vector128<uint> high) = Vector128.Widen( Vector128.WidenLower( packed ) );
		return Vector256.Create( low, high ).AsInt32();
	}

	// Per-lane mask where the given bit is set in the widened flags.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> HasFlag(
		Vector256<int> flags,
		Direction bit
	) {
		return ( ~Vector256.Equals( flags & Vector256.Create( (int)bit ), Vector256<int>.Zero ) ).AsSingle();
	}

	// Per-lane mask where the byte at each of 8 consecutive positions is non-zero.
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static Vector256<float> IsSet(
		ref byte values,
		int index
	) {
		return ( ~Vector256.Equals( LoadFlags( ref values, index ), Vector256<int>.Zero ) ).AsSingle();
	}

}
