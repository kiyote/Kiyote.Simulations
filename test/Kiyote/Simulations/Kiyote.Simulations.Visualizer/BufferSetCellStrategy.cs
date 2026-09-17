using System.Diagnostics.CodeAnalysis;
using Kiyote.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Visualizer;

[ExcludeFromCodeCoverage]
internal readonly struct BufferSetCellStrategy : ICallbackStrategy<double, double> {

	private readonly IBuffer<double> _buffer;

	public BufferSetCellStrategy(
		IBuffer<double> buffer
	) {
		_buffer = buffer;
	}

	public void Callback(
		GridCell<double> cell,
		double value
	) {
		_buffer[cell.Column, cell.Row] = value;
	}

}
