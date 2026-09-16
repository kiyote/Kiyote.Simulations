using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

internal readonly struct PressureFlowStrategy : IFlowStrategy<double, double> {

	private readonly double _viscosity;

	public PressureFlowStrategy() : this( 1.0 ) { }

	public PressureFlowStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination
	) {
		return ( source.Cell - destination.Cell ) * _viscosity / 2;
	}

	public double Combine(
		double left,
		double right
	) {
		return left + right;
	}

	public double Negate(
		double value
	) {
		return -value;
	}

	public double Apply(
		GridCell<double> cell,
		double delta
	) {
		return cell.Cell + delta;
	}

}

internal readonly struct AlwaysPassableStrategy : ICellStrategy<double, bool> {

	public bool Evaluate(
		GridCell<double> cell
	) {
		return true;
	}

}

internal readonly struct FixedGridSetCellStrategy : ICallbackStrategy<double, double> {

	private readonly FixedGrid<double> _grid;

	public FixedGridSetCellStrategy(
		FixedGrid<double> grid
	) {
		_grid = grid;
	}

	public void Callback(
		GridCell<double> cell,
		double value
	) {
		_grid.Set( cell.Column, cell.Row, value );
	}

}

[MemoryDiagnoser( false )]
public class GridFlowBenchmarks {

	private const int Size = 100;

	private readonly IGridFlow _gridFlow;
	private readonly FixedGrid<double> _grid;
	private readonly PressureFlowStrategy _strategy;
	private readonly AlwaysPassableStrategy _isPassable;
	private readonly FixedGridSetCellStrategy _setCell;

	public GridFlowBenchmarks() {
		_gridFlow = new GridFlow();
		_grid = CreateGrid();
		_strategy = new PressureFlowStrategy( 0.5 );
		_isPassable = new AlwaysPassableStrategy();
		_setCell = new FixedGridSetCellStrategy( _grid );
	}

	[Benchmark]
	public void Flow() {
		_gridFlow.Flow<double, double, PressureFlowStrategy, AlwaysPassableStrategy, FixedGridSetCellStrategy>( _grid, _strategy, _isPassable, _setCell );
	}

	private static FixedGrid<double> CreateGrid() {
		double[][] cells = new double[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new double[Size];
			for( int row = 0; row < Size; row++ ) {
				cells[column][row] = ( column == Size / 2 && row == Size / 2 ) ? 1000d : 0d;
			}
		}
		return new FixedGrid<double>( cells, Size, Size );
	}

}
