using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations;

public sealed class DoubleBufferedGrid<TState> {

	private bool _swapped;
	private readonly IGrid<TState> _a;
	private readonly IGrid<TState> _b;

	public DoubleBufferedGrid(
		IGrid<TState> a,
		IGrid<TState> b
	) {
		_a = a;
		_b = b;
	}

	public IGrid<TState> Source => _swapped ? _b : _a;

	public IGrid<TState> Destination => _swapped ? _a : _b;

	public void Swap() {
		_swapped = !_swapped;
	}

}
