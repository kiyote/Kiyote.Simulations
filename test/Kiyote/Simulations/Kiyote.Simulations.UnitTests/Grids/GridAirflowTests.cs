using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

[ExcludeFromCodeCoverage]
internal readonly struct AirflowForcingStrategy : IPressureForcingStrategy {

	private readonly double _gain;

	public AirflowForcingStrategy(
		double gain
	) {
		_gain = gain;
	}

	public Velocity CalculateForce(
		GridCell<double> source,
		GridCell<double> destination
	) {
		double deltaColumn = destination.Column - source.Column;
		double deltaRow = destination.Row - source.Row;
		double difference = source.Cell - destination.Cell;
		return new Velocity( deltaColumn * difference * _gain, deltaRow * difference * _gain );
	}

	public Velocity Combine(
		Velocity left,
		Velocity right
	) {
		return left + right;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct AirflowVelocityAccessor : IVelocityAccessor<Velocity> {

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
internal readonly struct AirflowVelocitySetCellStrategy : ICallbackStrategy<Velocity, Velocity> {

	private readonly TestGrid<Velocity> _grid;

	public AirflowVelocitySetCellStrategy(
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
internal readonly struct AirflowNoBoundaryStrategy : IBoundaryStrategy {

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
internal readonly struct AirflowWallBoundaryStrategy : IBoundaryStrategy {

	private readonly HashSet<(int Column, int Row)> _walls;

	public AirflowWallBoundaryStrategy(
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

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridAirflowTests {

	private IGridAirflow _airflow;

	[SetUp]
	public void SetUp() {
		_airflow = new GridAirflow( new GridFluid(), new GridPressure( new GridDiffusion() ) );
	}

	private static TestGrid<Velocity> CreateVelocityGrid(
		int width,
		int height
	) {
		TestGrid<Velocity> grid = new( 0, 0, width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.Set( column, row, Velocity.Zero );
			}
		}
		return grid;
	}

	private static TestGrid<double> CreatePressureGrid(
		int width,
		int height,
		double fill
	) {
		TestGrid<double> grid = new( 0, 0, width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.Set( column, row, fill );
			}
		}
		return grid;
	}

	[Test]
	public void ApplyPressureForcing_HigherPressureNeighbor_PushesVelocityAway() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 1 );
		TestGrid<double> pressure = CreatePressureGrid( 3, 1, 0.0 );
		pressure.Set( 0, 0, 10.0 );

		_airflow.ApplyPressureForcing<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowNoBoundaryStrategy, AirflowVelocitySetCellStrategy>(
			velocity,
			new AirflowVelocityAccessor(),
			pressure,
			new AirflowForcingStrategy( 1.0 ),
			new AirflowNoBoundaryStrategy(),
			new AirflowVelocitySetCellStrategy( velocity ),
			1.0
		);

		// The high-pressure cell at column 0 should push outward (positive X)
		// while its lower-pressure neighbor at column 1 should be pushed in
		// the same direction (away from the high-pressure source).
		Assert.That( velocity[0, 0].X, Is.GreaterThan( 0.0 ) );
		Assert.That( velocity[1, 0].X, Is.GreaterThan( 0.0 ) );
	}

	[Test]
	public void ApplyPressureForcing_UniformPressure_NoVelocityChange() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 3 );
		TestGrid<double> pressure = CreatePressureGrid( 3, 3, 5.0 );

		_airflow.ApplyPressureForcing<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowNoBoundaryStrategy, AirflowVelocitySetCellStrategy>(
			velocity,
			new AirflowVelocityAccessor(),
			pressure,
			new AirflowForcingStrategy( 1.0 ),
			new AirflowNoBoundaryStrategy(),
			new AirflowVelocitySetCellStrategy( velocity ),
			1.0
		);

		for( int row = 0; row < 3; row++ ) {
			for( int column = 0; column < 3; column++ ) {
				Assert.That( velocity[column, row], Is.EqualTo( Velocity.Zero ) );
			}
		}
	}

	[Test]
	public void ApplyPressureForcing_SolidCell_IsSkipped() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 1 );
		TestGrid<double> pressure = CreatePressureGrid( 3, 1, 0.0 );
		pressure.Set( 0, 0, 10.0 );
		AirflowWallBoundaryStrategy boundary = new( [ ( 1, 0 ) ] );

		_airflow.ApplyPressureForcing<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowWallBoundaryStrategy, AirflowVelocitySetCellStrategy>(
			velocity,
			new AirflowVelocityAccessor(),
			pressure,
			new AirflowForcingStrategy( 1.0 ),
			boundary,
			new AirflowVelocitySetCellStrategy( velocity ),
			1.0
		);

		Assert.That( velocity[1, 0], Is.EqualTo( Velocity.Zero ) );
	}

	[Test]
	public void ApplyPressureForcing_NullGrid_ThrowsArgumentNullException() {
		TestGrid<double> pressure = CreatePressureGrid( 3, 3, 0.0 );

		Assert.Throws<ArgumentNullException>( () => {
			_airflow.ApplyPressureForcing<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowNoBoundaryStrategy, AirflowVelocitySetCellStrategy>(
				null,
				new AirflowVelocityAccessor(),
				pressure,
				new AirflowForcingStrategy( 1.0 ),
				new AirflowNoBoundaryStrategy(),
				default,
				1.0
			);
		} );
	}

	[Test]
	public void ApplyPressureForcing_NullPressureGrid_ThrowsArgumentNullException() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 3, 3 );

		Assert.Throws<ArgumentNullException>( () => {
			_airflow.ApplyPressureForcing<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowNoBoundaryStrategy, AirflowVelocitySetCellStrategy>(
				velocity,
				new AirflowVelocityAccessor(),
				null,
				new AirflowForcingStrategy( 1.0 ),
				new AirflowNoBoundaryStrategy(),
				new AirflowVelocitySetCellStrategy( velocity ),
				1.0
			);
		} );
	}

	[Test]
	public void Advance_PressureImbalance_ProducesGasFlowTowardLowerPressure() {
		TestGrid<Velocity> velocity = CreateVelocityGrid( 5, 1 );
		TestGrid<double> pressure = CreatePressureGrid( 5, 1, 0.0 );
		pressure.Set( 0, 0, 100.0 );

		TestGrid<double> concentration = CreatePressureGrid( 5, 1, 0.0 );
		concentration.Set( 0, 0, 100.0 );
		GasSpecies<double, AirflowScalarSetCellStrategy> gas = new(
			concentration,
			new AirflowScalarSetCellStrategy( concentration )
		);

		_airflow.Advance<Velocity, AirflowVelocityAccessor, AirflowForcingStrategy, AirflowVelocityDiffusionStrategy, double, AirflowCombinedSampler, AirflowNoBoundaryStrategy, AirflowVelocitySetCellStrategy, AirflowScalarSetCellStrategy>(
			velocity,
			new AirflowVelocityAccessor(),
			pressure,
			new AirflowForcingStrategy( 1.0 ),
			new AirflowVelocityDiffusionStrategy( 0.0 ),
			[ gas ],
			new AirflowCombinedSampler(),
			new AirflowNoBoundaryStrategy(),
			new AirflowVelocitySetCellStrategy( velocity ),
			1.0
		);

		Assert.That( concentration[1, 0], Is.GreaterThan( 0.0 ) );
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct AirflowScalarSetCellStrategy : ICallbackStrategy<double, double> {

	private readonly TestGrid<double> _grid;

	public AirflowScalarSetCellStrategy(
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

[ExcludeFromCodeCoverage]
internal readonly struct AirflowVelocityDiffusionStrategy : IDiffusionStrategy<Velocity, Velocity> {

	private readonly double _viscosity;

	public AirflowVelocityDiffusionStrategy(
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
		if( divisor == 0 ) {
			return Velocity.Zero;
		}
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
internal readonly struct AirflowCombinedSampler : IGridSampler<double, Velocity>, IGridSampler<double, double> {

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
