using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.Benchmarks;

internal readonly struct FluidVelocityAccessorStrategy : IVelocityAccessor<Velocity> {

	public Velocity GetVelocity(
		Velocity cell
	) {
		return cell;
	}

	public Velocity WithVelocity(
		Velocity cell,
		Velocity velocity
	) {
		return velocity;
	}

}

internal readonly struct FluidVelocityDiffusionStrategy : IDiffusionStrategy<Velocity, Velocity> {

	private readonly double _viscosity;

	public FluidVelocityDiffusionStrategy() : this( 1.0 ) { }

	public FluidVelocityDiffusionStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public Velocity CalculateTransfer(
		GridCell<Velocity> source,
		GridCell<Velocity> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		double divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * ( _viscosity / divisor );
	}

	public Velocity Combine(
		Velocity left,
		Velocity right
	) {
		return left + right;
	}

	public Velocity Negate(
		Velocity value
	) {
		return -value;
	}

	public Velocity Apply(
		GridCell<Velocity> cell,
		Velocity delta
	) {
		return cell.Cell + delta;
	}

}

internal readonly struct FluidBilinearSampler : IGridSampler<double, Velocity>, IGridSampler<double, double> {

	Velocity IGridSampler<double, Velocity>.Sample(
		IGrid<Velocity> grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		Velocity topLeft = grid[column0, row0];
		Velocity topRight = grid[column1, row0];
		Velocity bottomLeft = grid[column0, row1];
		Velocity bottomRight = grid[column1, row1];

		Velocity top = Lerp( topLeft, topRight, columnFraction );
		Velocity bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	double IGridSampler<double, double>.Sample(
		IGrid<double> grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		double topLeft = grid[column0, row0];
		double topRight = grid[column1, row0];
		double bottomLeft = grid[column0, row1];
		double bottomRight = grid[column1, row1];

		double top = Lerp( topLeft, topRight, columnFraction );
		double bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	// Generic overloads: let struct-based IGrid<T> implementations (e.g. VelocityGridView) be
	// sampled without boxing, since grid is used through the TGrid constraint directly instead
	// of being converted to the IGrid<T> interface type.
	Velocity IGridSampler<double, Velocity>.Sample<TGrid>(
		TGrid grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		Velocity topLeft = grid[column0, row0];
		Velocity topRight = grid[column1, row0];
		Velocity bottomLeft = grid[column0, row1];
		Velocity bottomRight = grid[column1, row1];

		Velocity top = Lerp( topLeft, topRight, columnFraction );
		Velocity bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	double IGridSampler<double, double>.Sample<TGrid>(
		TGrid grid,
		double column,
		double row
	) {
		(int column0, int column1, double columnFraction) = Axis( column, grid.Column, grid.Width );
		(int row0, int row1, double rowFraction) = Axis( row, grid.Row, grid.Height );

		double topLeft = grid[column0, row0];
		double topRight = grid[column1, row0];
		double bottomLeft = grid[column0, row1];
		double bottomRight = grid[column1, row1];

		double top = Lerp( topLeft, topRight, columnFraction );
		double bottom = Lerp( bottomLeft, bottomRight, columnFraction );
		return Lerp( top, bottom, rowFraction );
	}

	private static (int Low, int High, double Fraction) Axis(
		double value,
		int origin,
		int length
	) {
		int max = origin + length - 1;
		double clamped = Math.Clamp( value, origin, max );
		int low = Math.Clamp( (int)Math.Floor( clamped ), origin, max );
		int high = Math.Clamp( low + 1, origin, max );
		double fraction = clamped - low;
		return (low, high, fraction);
	}

	private static Velocity Lerp(
		Velocity left,
		Velocity right,
		double fraction
	) {
		return left + ( ( right - left ) * fraction );
	}

	private static double Lerp(
		double left,
		double right,
		double fraction
	) {
		return left + ( ( right - left ) * fraction );
	}

}

internal readonly struct AlwaysOpenBoundaryStrategy : IBoundaryStrategy {

	public bool IsSolid(
		int column,
		int row
	) {
		return false;
	}

	public Velocity ReflectVelocity(
		Velocity velocity,
		int column,
		int row
	) {
		return -velocity;
	}

}

internal readonly struct FixedGridVelocitySetCellStrategy : ICallbackStrategy<Velocity, Velocity> {

	private readonly FixedGrid<Velocity> _grid;

	public FixedGridVelocitySetCellStrategy(
		FixedGrid<Velocity> grid
	) {
		_grid = grid;
	}

	public void Callback(
		GridCell<Velocity> cell,
		Velocity value
	) {
		_grid.Set( cell.Column, cell.Row, value );
	}

}

[MemoryDiagnoser( false )]
public class GridFluidBenchmarks {

	private const int Size = 100;

	private readonly IGridFluid _gridFluid;
	private readonly FixedGrid<Velocity> _grid;
	private readonly FixedGrid<double> _concentration;
	private readonly FluidVelocityAccessorStrategy _velocityAccessor;
	private readonly FluidVelocityDiffusionStrategy _velocityDiffusion;
	private readonly FluidBilinearSampler _sampler;
	private readonly AlwaysOpenBoundaryStrategy _boundary;
	private readonly FixedGridVelocitySetCellStrategy _setCell;
	private readonly FixedGridSetCellStrategy _concentrationSetCell;
	private readonly List<GasSpecies<double, FixedGridSetCellStrategy>> _gases;

	private const int PrimingSteps = 100;

	public GridFluidBenchmarks() {
		_gridFluid = new GridFluid();
		_grid = CreateGrid();
		_concentration = CreateConcentration();
		_velocityAccessor = new FluidVelocityAccessorStrategy();
		_velocityDiffusion = new FluidVelocityDiffusionStrategy( 0.5 );
		_sampler = new FluidBilinearSampler();
		_boundary = new AlwaysOpenBoundaryStrategy();
		_setCell = new FixedGridVelocitySetCellStrategy( _grid );
		_concentrationSetCell = new FixedGridSetCellStrategy( _concentration );
		_gases = [
			new GasSpecies<double, FixedGridSetCellStrategy>( _concentration, _concentrationSetCell )
		];

		for( int i = 0; i < PrimingSteps; i++ ) {
			StepVelocity();
			AdvectGases();
		}
	}

	[Benchmark]
	public void StepVelocity() {
		_gridFluid.StepVelocity<Velocity, FluidVelocityAccessorStrategy, FluidVelocityDiffusionStrategy, FluidBilinearSampler, AlwaysOpenBoundaryStrategy, FixedGridVelocitySetCellStrategy>(
			_grid, _velocityAccessor, _velocityDiffusion, _sampler, _boundary, _setCell, 1.0 );
	}

	[Benchmark]
	public void AdvectGases() {
		_gridFluid.AdvectGases<Velocity, FluidVelocityAccessorStrategy, double, FluidBilinearSampler, AlwaysOpenBoundaryStrategy, FixedGridSetCellStrategy>(
			_grid, _velocityAccessor, _gases, _sampler, _boundary, 1.0 );
	}

	private static FixedGrid<Velocity> CreateGrid() {
		Velocity[][] cells = new Velocity[Size][];
		for( int column = 0; column < Size; column++ ) {
			cells[column] = new Velocity[Size];
			for( int row = 0; row < Size; row++ ) {
				cells[column][row] = ( column == Size / 2 && row == Size / 2 ) ? new Velocity( 5.0, 0.0 ) : Velocity.Zero;
			}
		}
		return new FixedGrid<Velocity>( cells, Size, Size );
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
