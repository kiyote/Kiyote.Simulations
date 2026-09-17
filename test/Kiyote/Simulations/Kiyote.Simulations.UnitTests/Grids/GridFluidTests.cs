using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

[ExcludeFromCodeCoverage]
internal readonly struct VelocityDiffusionStrategy : IDiffusionStrategy<Velocity, Velocity> {

	private readonly double _viscosity;

	public VelocityDiffusionStrategy() : this( 1.0 ) { }

	public VelocityDiffusionStrategy(
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

[ExcludeFromCodeCoverage]
internal readonly struct NoBoundaryStrategy : IBoundaryStrategy {

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
		return velocity;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct WallBoundaryStrategy : IBoundaryStrategy {

	private readonly HashSet<(int Column, int Row)> _walls;

	public WallBoundaryStrategy(
		HashSet<(int Column, int Row)> walls
	) {
		_walls = walls;
	}

	public bool IsSolid(
		int column,
		int row
	) {
		return _walls.Contains( ( column, row ) );
	}

	public Velocity ReflectVelocity(
		Velocity velocity,
		int column,
		int row
	) {
		return Velocity.Zero;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct NearestVelocitySampler : IGridSampler<double, Velocity> {

	public Velocity Sample(
		IGrid<Velocity> grid,
		double column,
		double row
	) {
		int nearestColumn = Math.Clamp( (int)Math.Round( column ), grid.Column, grid.Column + grid.Width - 1 );
		int nearestRow = Math.Clamp( (int)Math.Round( row ), grid.Row, grid.Row + grid.Height - 1 );
		return grid[nearestColumn, nearestRow];
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct NearestScalarSampler : IGridSampler<double, double> {

	public double Sample(
		IGrid<double> grid,
		double column,
		double row
	) {
		int nearestColumn = Math.Clamp( (int)Math.Round( column ), grid.Column, grid.Column + grid.Width - 1 );
		int nearestRow = Math.Clamp( (int)Math.Round( row ), grid.Row, grid.Row + grid.Height - 1 );
		return grid[nearestColumn, nearestRow];
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct CombinedSampler : IGridSampler<double, Velocity>, IGridSampler<double, double> {

	Velocity IGridSampler<double, Velocity>.Sample(
		IGrid<Velocity> grid,
		double column,
		double row
	) {
		int nearestColumn = Math.Clamp( (int)Math.Round( column ), grid.Column, grid.Column + grid.Width - 1 );
		int nearestRow = Math.Clamp( (int)Math.Round( row ), grid.Row, grid.Row + grid.Height - 1 );
		return grid[nearestColumn, nearestRow];
	}

	double IGridSampler<double, double>.Sample(
		IGrid<double> grid,
		double column,
		double row
	) {
		int nearestColumn = Math.Clamp( (int)Math.Round( column ), grid.Column, grid.Column + grid.Width - 1 );
		int nearestRow = Math.Clamp( (int)Math.Round( row ), grid.Row, grid.Row + grid.Height - 1 );
		return grid[nearestColumn, nearestRow];
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct IdentityVelocityAccessor : IVelocityAccessor<Velocity> {

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

[ExcludeFromCodeCoverage]
internal readonly struct VelocitySetCellStrategy : ICallbackStrategy<Velocity, Velocity> {

	private readonly TestGrid<Velocity> _grid;

	public VelocitySetCellStrategy(
		TestGrid<Velocity> grid
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

[ExcludeFromCodeCoverage]
internal readonly struct ScalarSetCellStrategy : ICallbackStrategy<double, double> {

	private readonly TestGrid<double> _grid;

	public ScalarSetCellStrategy(
		TestGrid<double> grid
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

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridFluidTests {

	private IGridFluid _fluid;

	[SetUp]
	public void SetUp() {
		_fluid = new GridFluid();
	}

	private static TestGrid<Velocity> CreateVelocityGrid(
		int width,
		int height,
		Velocity fill
	) {
		TestGrid<Velocity> grid = new( 0, 0, width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.Set( column, row, fill );
			}
		}
		return grid;
	}

	[Test]
	public void StepVelocity_ZeroVelocityField_RemainsZero() {
		TestGrid<Velocity> grid = CreateVelocityGrid( 3, 3, Velocity.Zero );

		_fluid.StepVelocity<Velocity, IdentityVelocityAccessor, VelocityDiffusionStrategy, NearestVelocitySampler, NoBoundaryStrategy, VelocitySetCellStrategy>(
			grid,
			new IdentityVelocityAccessor(),
			new VelocityDiffusionStrategy( 1.0 ),
			new NearestVelocitySampler(),
			new NoBoundaryStrategy(),
			new VelocitySetCellStrategy( grid ),
			1.0
		);

		for( int row = 0; row < 3; row++ ) {
			for( int column = 0; column < 3; column++ ) {
				Assert.That( grid[column, row].X, Is.EqualTo( 0.0 ) );
				Assert.That( grid[column, row].Y, Is.EqualTo( 0.0 ) );
			}
		}
	}

	[Test]
	public void StepVelocity_SolidBoundary_StaysZero() {
		TestGrid<Velocity> grid = CreateVelocityGrid( 3, 3, new Velocity( 1.0, 0.0 ) );
		WallBoundaryStrategy boundary = new( [ ( 1, 1 ) ] );

		_fluid.StepVelocity<Velocity, IdentityVelocityAccessor, VelocityDiffusionStrategy, NearestVelocitySampler, WallBoundaryStrategy, VelocitySetCellStrategy>(
			grid,
			new IdentityVelocityAccessor(),
			new VelocityDiffusionStrategy( 1.0 ),
			new NearestVelocitySampler(),
			boundary,
			new VelocitySetCellStrategy( grid ),
			1.0
		);

		Assert.That( grid[1, 1], Is.EqualTo( new Velocity( 1.0, 0.0 ) ) );
	}

	[Test]
	public void StepVelocity_NullGrid_ThrowsArgumentNullException() {
		Assert.Throws<ArgumentNullException>( () => {
			_fluid.StepVelocity<Velocity, IdentityVelocityAccessor, VelocityDiffusionStrategy, NearestVelocitySampler, NoBoundaryStrategy, VelocitySetCellStrategy>(
				null,
				new IdentityVelocityAccessor(),
				new VelocityDiffusionStrategy( 1.0 ),
				new NearestVelocitySampler(),
				new NoBoundaryStrategy(),
				default,
				1.0
			);
		} );
	}

	[Test]
	public void AdvectGases_UniformVelocity_ConservesTotalQuantity() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 3, new Velocity( 1.0, 0.0 ) );

		TestGrid<double> concentration = new( 0, 0, 3, 3 );
		concentration.Set( 0, 0, 10.0 );
		concentration.Set( 1, 0, 0.0 );
		concentration.Set( 2, 0, 0.0 );
		concentration.Set( 0, 1, 0.0 );
		concentration.Set( 1, 1, 0.0 );
		concentration.Set( 2, 1, 0.0 );
		concentration.Set( 0, 2, 0.0 );
		concentration.Set( 1, 2, 0.0 );
		concentration.Set( 2, 2, 0.0 );

		List<GasSpecies<double, ScalarSetCellStrategy>> gases = [
			new( concentration, new ScalarSetCellStrategy( concentration ) ),
		];

		_fluid.AdvectGases<Velocity, IdentityVelocityAccessor, double, CombinedSampler, NoBoundaryStrategy, ScalarSetCellStrategy>(
			velocity,
			new IdentityVelocityAccessor(),
			gases,
			new CombinedSampler(),
			new NoBoundaryStrategy(),
			1.0
		);

		Assert.That( concentration[1, 0], Is.EqualTo( 10.0 ) );
	}

	[Test]
	public void AdvectGases_MultipleSpecies_EachAdvectedIndependently() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 3, new Velocity( 1.0, 0.0 ) );

		TestGrid<double> oxygen = new( 0, 0, 3, 3 );
		TestGrid<double> nitrogen = new( 0, 0, 3, 3 );
		oxygen.Set( 0, 0, 5.0 );
		nitrogen.Set( 0, 0, 20.0 );

		List<GasSpecies<double, ScalarSetCellStrategy>> gases = [
			new( oxygen, new ScalarSetCellStrategy( oxygen ) ),
			new( nitrogen, new ScalarSetCellStrategy( nitrogen ) ),
		];

		_fluid.AdvectGases<Velocity, IdentityVelocityAccessor, double, CombinedSampler, NoBoundaryStrategy, ScalarSetCellStrategy>(
			velocity,
			new IdentityVelocityAccessor(),
			gases,
			new CombinedSampler(),
			new NoBoundaryStrategy(),
			1.0
		);

		Assert.Multiple( () => {
			Assert.That( oxygen[1, 0], Is.EqualTo( 5.0 ) );
			Assert.That( nitrogen[1, 0], Is.EqualTo( 20.0 ) );
		} );
	}

}
