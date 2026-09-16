using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer;

[ExcludeFromCodeCoverage]
internal readonly struct BufferSetCellStrategy : ICallbackStrategy<byte, byte> {

	private readonly IBuffer<byte> _buffer;

	public BufferSetCellStrategy(
		IBuffer<byte> buffer
	) {
		_buffer = buffer;
	}

	public void Callback(
		GridCell<byte> cell,
		byte value
	) {
		_buffer[cell.Column, cell.Row] = value;
	}

}
