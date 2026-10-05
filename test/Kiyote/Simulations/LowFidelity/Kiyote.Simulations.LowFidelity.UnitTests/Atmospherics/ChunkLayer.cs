using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

// A single 16x16 chunk (slot 0) with a 1-cell halo, so stages can be driven directly.
// Moq cannot set up Span-returning members, hence a hand-written layer.
internal sealed class ChunkLayer<T> : IGridLayer<T> {

	public const int Size = 16;
	public const int HaloSize = 1;
	public const int RowStride = Size + ( 2 * HaloSize );

	private readonly T[] _cells = new T[RowStride * RowStride];

	public bool Dirty { get; private set; }

	public Span<T> Cells => _cells;

	public IGridChunkLayout Space => null;

	public int Halo => HaloSize;

	public int Stride => RowStride;

	public int ChunkLength => _cells.Length;

	public ref T this[int x, int y] => ref _cells[IndexOf( 0, x, y )];

	public Span<T> GetChunk(
		int slot
	) {
		return _cells;
	}

	public ref T GetChunkReference(
		int slot
	) {
		return ref _cells[0];
	}

	public Span<T> GetRow(
		int slot,
		int row
	) {
		return _cells.AsSpan( IndexOf( slot, 0, row ), Size );
	}

	public int IndexOf(
		int slot,
		int x,
		int y
	) {
		return ( ( y + HaloSize ) * RowStride ) + x + HaloSize;
	}

	public void Fill(
		T value
	) {
		Array.Fill( _cells, value );
	}

	public bool IsDirty(
		int slot
	) {
		return Dirty;
	}

	public void MarkDirty(
		int slot
	) {
		Dirty = true;
	}

	public void ClearDirty() {
		Dirty = false;
	}

	public void ExchangeHalos() {
	}

	public void ExchangeHalo(
		int slot
	) {
	}

}

internal static class ChunkMask {

	// Every cell valid: exercises the full-row SIMD paths.
	public static ulong[] Full() {
		return [ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue];
	}

	// One valid cell: exercises the scalar per-cell paths.
	public static ulong[] Cell(
		int x,
		int y
	) {
		ulong[] mask = new ulong[4];
		int bit = ( y * ChunkLayer<float>.Size ) + x;
		mask[bit >> 6] = 1UL << ( bit & 63 );
		return mask;
	}

}
