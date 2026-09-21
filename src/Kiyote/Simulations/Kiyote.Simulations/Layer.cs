using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations;

public sealed record Layer<TCell, TPassability, TState>(
	string Name,
	IGrid<TCell> Grid,
	TPassability Passability,
	Material Material,
	DoubleBufferedGrid<TState> State
)
	where TPassability : ICellStrategy<TCell, bool>;
