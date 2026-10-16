namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

// Storage shaped like the spans the stage kernels take, indexed by chunk-local (x, y).
// Padded buffers also accept -1 and ChunkSize on either axis for the border cells that
// GridChunkNeighbourhood.CopyPadded would copy from neighbouring chunks.
internal sealed class ChunkBuffer<T> {

	private readonly T[] _values;
	private readonly bool _padded;

	private ChunkBuffer(
		bool padded
	) {
		_padded = padded;
		_values = new T[padded ? AtmosphereSimd.PaddedArea : AtmosphereSimd.ChunkArea];
	}

	public Span<T> Span => _values;

	public ref T this[int x, int y] => ref _values[_padded ? AtmosphereSimd.PaddedIndex( x, y ) : AtmosphereSimd.ChunkIndex( x, y )];

	public static ChunkBuffer<T> Chunk() {
		return new ChunkBuffer<T>( padded: false );
	}

	public static ChunkBuffer<T> Padded() {
		return new ChunkBuffer<T>( padded: true );
	}

	public void Fill(
		T value
	) {
		Array.Fill( _values, value );
	}

}

internal static class ChunkMask {

	// Every cell present: exercises the full-row SIMD paths.
	public static ulong[] Full() {
		ulong[] mask = new ulong[AtmosphereSimd.ChunkSize];
		Array.Fill( mask, AtmosphereSimd.FullRow );
		return mask;
	}

	// One present cell: exercises the scalar per-cell paths.
	public static ulong[] Cell(
		int x,
		int y
	) {
		ulong[] mask = new ulong[AtmosphereSimd.ChunkSize];
		mask[y] = 1UL << x;
		return mask;
	}

	public static ulong[] Create(
		bool full,
		int x,
		int y
	) {
		return full ? Full() : Cell( x, y );
	}

}
