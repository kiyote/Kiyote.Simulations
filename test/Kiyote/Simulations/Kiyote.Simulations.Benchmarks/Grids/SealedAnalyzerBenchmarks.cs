using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

internal readonly struct WallPassabilityStrategy : ICellStrategy<char, bool> {

	public bool Evaluate(
		GridCell<char> cell
	) {
		return cell.Cell != '#';
	}

}

[MemoryDiagnoser( false )]
public class SealedAnalyzerBenchmarks {

	private const int Size = 100;

	private readonly ISealedAnalyzer _analyzer;
	private readonly IGrid<char> _grid;
	private readonly WallPassabilityStrategy _isPassable;

	public SealedAnalyzerBenchmarks() {
		_analyzer = new SealedAnalyzer();
		_grid = CreateGrid();
		_isPassable = new WallPassabilityStrategy();
	}

	[Benchmark]
	public bool IsSealed() {
		return _analyzer.IsSealed( _grid, Size / 2, Size / 2, _isPassable );
	}

	private static IGrid<char> CreateGrid() {
		char[][] cells = new char[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new char[Size];
			for( int row = 0; row < Size; row++ ) {
				bool isWall = column == 0
					|| row == 0
					|| column == Size - 1
					|| row == Size - 1
					|| ( column % 7 == 0 && row % 5 == 0 );
				cells[column][row] = isWall ? '#' : '.';
			}
		}
		return new FixedGrid<char>( cells, Size, Size );
	}

}
