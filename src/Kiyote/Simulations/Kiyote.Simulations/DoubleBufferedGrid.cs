using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations;

public sealed class DoubleBufferedGrid<TState> {

	private bool _swapped;
	private readonly IMutableGrid<TState> _a;
	private readonly IMutableGrid<TState> _b;

	public DoubleBufferedGrid(
		IMutableGrid<TState> a,
		IMutableGrid<TState> b
	) {
		_a = a;
		_b = b;
	}

	public IMutableGrid<TState> Source => _swapped ? _b : _a;

	public IMutableGrid<TState> Destination => _swapped ? _a : _b;

	public void Swap() {
		_swapped = !_swapped;
	}

}
