using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

internal readonly struct AirflowPressureForcingStrategy : IPressureForcingStrategy {

	private readonly double _strength;

	public AirflowPressureForcingStrategy() : this( 1.0 ) { }

	public AirflowPressureForcingStrategy(
		double strength
	) {
		_strength = strength;
	}

	public Velocity CalculateForce(
		GridCell<double> source,
		GridCell<double> destination
	) {
		double delta = ( source.Cell - destination.Cell ) * _strength;
		int deltaColumn = Math.Sign( destination.Column - source.Column );
		int deltaRow = Math.Sign( destination.Row - source.Row );
		return new Velocity( delta * deltaColumn, delta * deltaRow );
	}

	public Velocity Combine(
		Velocity left,
		Velocity right
	) {
		return left + right;
	}

}

[MemoryDiagnoser( false )]
public class GridAirflowBenchmarks {

	private const int Size = 100;

	private readonly IGridAirflow _gridAirflow;
	private readonly FixedGrid<Velocity> _grid;
	private readonly FixedGrid<double> _totalPressure;
	private readonly FixedGrid<double> _concentration;
	private readonly FluidVelocityAccessorStrategy _velocityAccessor;
	private readonly AirflowPressureForcingStrategy _forcingStrategy;
	private readonly FluidVelocityDiffusionStrategy _velocityDiffusion;
	private readonly FluidBilinearSampler _sampler;
	private readonly AlwaysOpenBoundaryStrategy _boundary;
	private readonly FixedGridVelocitySetCellStrategy _setCell;
	private readonly FixedGridSetCellStrategy _concentrationSetCell;
	private readonly List<GasSpecies<double, FixedGridSetCellStrategy>> _gases;

	private const int PrimingSteps = 100;

	public GridAirflowBenchmarks() {
		_gridAirflow = new GridAirflow( new GridFluid(), new GridPressure( new GridDiffusion() ) );
		_grid = CreateGrid();
		_totalPressure = CreatePressure();
		_concentration = CreateConcentration();
		_velocityAccessor = new FluidVelocityAccessorStrategy();
		_forcingStrategy = new AirflowPressureForcingStrategy( 0.5 );
		_velocityDiffusion = new FluidVelocityDiffusionStrategy( 0.5 );
		_sampler = new FluidBilinearSampler();
		_boundary = new AlwaysOpenBoundaryStrategy();
		_setCell = new FixedGridVelocitySetCellStrategy( _grid );
		_concentrationSetCell = new FixedGridSetCellStrategy( _concentration );
		_gases = [
			new GasSpecies<double, FixedGridSetCellStrategy>( _concentration, _concentrationSetCell )
		];

		for( int i = 0; i < PrimingSteps; i++ ) {
			Advance();
		}
	}

	[Benchmark]
	public void ApplyPressureForcing() {
		_gridAirflow.ApplyPressureForcing<Velocity, FluidVelocityAccessorStrategy, AirflowPressureForcingStrategy, AlwaysOpenBoundaryStrategy, FixedGridVelocitySetCellStrategy>(
			_grid, _velocityAccessor, _totalPressure, _forcingStrategy, _boundary, _setCell, 1.0 );
	}

	[Benchmark]
	public void Advance() {
		_gridAirflow.Advance<Velocity, FluidVelocityAccessorStrategy, AirflowPressureForcingStrategy, FluidVelocityDiffusionStrategy, double, FluidBilinearSampler, AlwaysOpenBoundaryStrategy, FixedGridVelocitySetCellStrategy, FixedGridSetCellStrategy>(
			_grid, _velocityAccessor, _totalPressure, _forcingStrategy, _velocityDiffusion, _gases, _sampler, _boundary, _setCell, 1.0 );
	}

	private static FixedGrid<Velocity> CreateGrid() {
		Velocity[][] cells = new Velocity[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new Velocity[Size];
		}
		return new FixedGrid<Velocity>( cells, Size, Size );
	}

	private static FixedGrid<double> CreatePressure() {
		double[][] cells = new double[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new double[Size];
			for( int row = 0; row < Size; row++ ) {
				cells[column][row] = ( column == Size / 2 && row == Size / 2 ) ? 1000d : 0d;
			}
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
