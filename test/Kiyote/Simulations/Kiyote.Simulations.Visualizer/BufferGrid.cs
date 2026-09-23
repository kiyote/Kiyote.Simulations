using System.Numerics;
using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer;

/// <summary>
/// A dense, fixed-size <see cref="IGrid{T}"/> used to benchmark against a
/// consistent 100x100 layout without the overhead of attach/detach support.
/// </summary>
internal sealed class BufferGrid<T> : INumericBuffer<T>, IMutableGrid<T> where T: struct, INumber<T> {

	private readonly IBuffer<T> _buffer;

	public BufferGrid(
		IBuffer<T> buffer
	) {
		_buffer = buffer;
	}

	int IBuffer<T>.Columns => _buffer.Columns;

	int IBuffer<T>.Rows => _buffer.Rows;

	int IGrid<T>.Column => 0;

	int IGrid<T>.Row => 0;

	int IGrid<T>.Width => _buffer.Columns;

	int IGrid<T>.Height => _buffer.Rows;

	T IMutableGrid<T>.this[int column, int row] { get => _buffer[column, row]; set => _buffer[column, row] = value; }

	T IGrid<T>.this[int column, int row] => _buffer[column, row];

	T IBuffer<T>.this[int column, int row] { get => _buffer[column, row]; set => _buffer[column, row] = value; }

	Span<T> IBuffer<T>.GetRowSpan( int row ) {
		return _buffer.GetRowSpan( row );
	}

	bool IGrid<T>.TryAttach( IGrid<T> grid, int column, int row ) {
		throw new NotImplementedException();
	}

	bool IGrid<T>.TryDetach( IGrid<T> grid ) {
		throw new NotImplementedException();
	}

	IGrid<T>? IGrid<T>.GetGrid( int column, int row ) {
		throw new NotImplementedException();
	}

	IGrid<T>? IGrid<T>.GetGrid( int column, int row, bool recursive ) {
		throw new NotImplementedException();
	}

	void IGrid<T>.VisitGrids( int column, int row, Action<IGrid<T>, int, int> visitor ) {
		throw new NotImplementedException();
	}
}
