using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

internal readonly struct PressureDiffusionStrategy : IDiffusionStrategy<double, double> {

	private readonly double _viscosity;

	public PressureDiffusionStrategy() : this( 1.0 ) { }

	public PressureDiffusionStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * _viscosity / divisor;
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

internal readonly struct PressurePumpStrategy : IPumpStrategy<double, double> {

	private readonly int _column;
	private readonly int _row;
	private readonly double _rate;

	public PressurePumpStrategy(
		int column,
		int row,
		double rate
	) {
		_column = column;
		_row = row;
		_rate = rate;
	}

	public double Apply(
		GridCell<double> cell,
		double concentration,
		double timeStep
	) {
		if( cell.Column != _column || cell.Row != _row ) {
			return concentration;
		}
		return Math.Max( 0d, concentration + ( _rate * timeStep ) );
	}

}

[MemoryDiagnoser( false )]
public class GridPressureBenchmarks {

	private const int Size = 100;

	private readonly IGridPressure _gridPressure;
	private readonly FixedGrid<double> _grid;
	private readonly FixedGrid<double> _concentration;
	private readonly PressureDiffusionStrategy _diffusionStrategy;
	private readonly AlwaysPassableStrategy _isPassable;
	private readonly FixedGridSetCellStrategy _setCell;
	private readonly List<GasSpecies<double, FixedGridSetCellStrategy>> _gases;
	private readonly List<PressurePumpStrategy> _pumps;

	private const int PrimingSteps = 100;

	public GridPressureBenchmarks() {
		_gridPressure = new GridPressure( new GridDiffusion() );
		_grid = CreateGrid();
		_concentration = CreateConcentration();
		_diffusionStrategy = new PressureDiffusionStrategy( 0.5 );
		_isPassable = new AlwaysPassableStrategy();
		_setCell = new FixedGridSetCellStrategy( _concentration );
		_gases = [
			new GasSpecies<double, FixedGridSetCellStrategy>( _concentration, _setCell )
		];
		_pumps = [
			new PressurePumpStrategy( Size / 4, Size / 4, 10.0 )
		];

		for( int i = 0; i < PrimingSteps; i++ ) {
			Diffuse();
			Inject();
		}
	}

	[Benchmark]
	public void Diffuse() {
		_gridPressure.Diffuse<double, double, PressureDiffusionStrategy, AlwaysPassableStrategy, FixedGridSetCellStrategy>(
			_grid, _gases, _diffusionStrategy, _isPassable, 1.0 );
	}

	[Benchmark]
	public void Inject() {
		_gridPressure.Inject<double, double, PressurePumpStrategy, AlwaysPassableStrategy, FixedGridSetCellStrategy>(
			_grid, _gases[0], _pumps, _isPassable, 1.0 );
	}

	private static FixedGrid<double> CreateGrid() {
		double[][] cells = new double[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new double[Size];
		}
		return new FixedGrid<double>( cells, Size, Size );
	}

	private static FixedGrid<double> CreateConcentration() {
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
