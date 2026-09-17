using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

[ExcludeFromCodeCoverage]
internal readonly struct PressureDiffusionStrategy : IDiffusionStrategy<double, double> {

	private readonly double _rate;

	public PressureDiffusionStrategy() : this( 1.0 ) { }

	public PressureDiffusionStrategy(
		double rate
	) {
		_rate = rate;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * _rate / divisor;
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
		return Math.Max( 0.0, cell.Cell + delta );
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct PressureAlwaysPassableStrategy : ICellStrategy<double, bool> {

	public bool Evaluate(
		GridCell<double> cell
	) {
		return true;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct PressureWallPassableStrategy : ICellStrategy<double, bool> {

	private readonly HashSet<(int Column, int Row)> _walls;

	public PressureWallPassableStrategy(
		HashSet<(int Column, int Row)> walls
	) {
		_walls = walls;
	}

	public bool Evaluate(
		GridCell<double> cell
	) {
		return !_walls.Contains( ( cell.Column, cell.Row ) );
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct PressureScalarSetCellStrategy : ICallbackStrategy<double, double> {

	private readonly TestGrid<double> _grid;

	public PressureScalarSetCellStrategy(
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

// Injects a fixed amount per tick at a single cell.
[ExcludeFromCodeCoverage]
internal readonly struct FixedRatePumpStrategy : IPumpStrategy<double, double> {

	private readonly int _column;
	private readonly int _row;
	private readonly double _ratePerSecond;

	public FixedRatePumpStrategy(
		int column,
		int row,
		double ratePerSecond
	) {
		_column = column;
		_row = row;
		_ratePerSecond = ratePerSecond;
	}

	public double Apply(
		GridCell<double> cell,
		double concentration,
		double timeStep
	) {
		if( cell.Column != _column || cell.Row != _row ) {
			return concentration;
		}

		return Math.Max( 0.0, concentration + ( _ratePerSecond * timeStep ) );
	}

}

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridPressureTests {

	private IGridPressure _pressure;

	[SetUp]
	public void SetUp() {
		_pressure = new GridPressure( new GridDiffusion() );
	}

	[Test]
	public void Diffuse_TwoAdjacentCells_EqualizesTowardEachOther() {
		TestGrid<double> concentration = new( 0, 0, 2, 1 );
		concentration.Set( 0, 0, 10.0 );
		concentration.Set( 1, 0, 0.0 );

		List<GasSpecies<double, PressureScalarSetCellStrategy>> gases = [
			new( concentration, new PressureScalarSetCellStrategy( concentration ) ),
		];

		for( int i = 0; i < 20; i++ ) {
			_pressure.Diffuse<double, double, PressureDiffusionStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
				concentration,
				gases,
				new PressureDiffusionStrategy( 1.0 ),
				new PressureAlwaysPassableStrategy(),
				1.0
			);
		}

		Assert.Multiple( () => {
			Assert.That( concentration[0, 0], Is.EqualTo( 5.0 ).Within( 0.01 ) );
			Assert.That( concentration[1, 0], Is.EqualTo( 5.0 ).Within( 0.01 ) );
		} );
	}

	[Test]
	public void Diffuse_MultipleSpecies_EachDiffusesIndependently() {
		TestGrid<double> oxygen = new( 0, 0, 2, 1 );
		TestGrid<double> nitrogen = new( 0, 0, 2, 1 );
		oxygen.Set( 0, 0, 10.0 );
		nitrogen.Set( 0, 0, 40.0 );

		List<GasSpecies<double, PressureScalarSetCellStrategy>> gases = [
			new( oxygen, new PressureScalarSetCellStrategy( oxygen ) ),
			new( nitrogen, new PressureScalarSetCellStrategy( nitrogen ) ),
		];

		for( int i = 0; i < 20; i++ ) {
			_pressure.Diffuse<double, double, PressureDiffusionStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
				oxygen,
				gases,
				new PressureDiffusionStrategy( 1.0 ),
				new PressureAlwaysPassableStrategy(),
				1.0
			);
		}

		Assert.Multiple( () => {
			Assert.That( oxygen[1, 0], Is.EqualTo( 5.0 ).Within( 0.01 ) );
			Assert.That( nitrogen[1, 0], Is.EqualTo( 20.0 ).Within( 0.01 ) );
		} );
	}

	[Test]
	public void Diffuse_SolidCellBetween_BlocksFlow() {
		TestGrid<double> concentration = new( 0, 0, 3, 1 );
		concentration.Set( 0, 0, 10.0 );
		concentration.Set( 1, 0, 0.0 );
		concentration.Set( 2, 0, 0.0 );

		PressureWallPassableStrategy passability = new( [ ( 1, 0 ) ] );

		List<GasSpecies<double, PressureScalarSetCellStrategy>> gases = [
			new( concentration, new PressureScalarSetCellStrategy( concentration ) ),
		];

		_pressure.Diffuse<double, double, PressureDiffusionStrategy, PressureWallPassableStrategy, PressureScalarSetCellStrategy>(
			concentration,
			gases,
			new PressureDiffusionStrategy( 1.0 ),
			passability,
			1.0
		);

		Assert.That( concentration[2, 0], Is.EqualTo( 0.0 ) );
	}

	[Test]
	public void Diffuse_NullGrid_ThrowsArgumentNullException() {
		Assert.Throws<ArgumentNullException>( () => {
			_pressure.Diffuse<double, double, PressureDiffusionStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
				null,
				[],
				new PressureDiffusionStrategy( 1.0 ),
				new PressureAlwaysPassableStrategy(),
				1.0
			);
		} );
	}

	[Test]
	public void Inject_PumpAtCell_IncreasesConcentration() {
		TestGrid<double> concentration = new( 0, 0, 2, 1 );
		concentration.Set( 0, 0, 0.0 );
		concentration.Set( 1, 0, 0.0 );

		GasSpecies<double, PressureScalarSetCellStrategy> gas = new( concentration, new PressureScalarSetCellStrategy( concentration ) );
		List<FixedRatePumpStrategy> pumps = [ new FixedRatePumpStrategy( 0, 0, 2.0 ) ];

		_pressure.Inject<double, double, FixedRatePumpStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
			concentration,
			gas,
			pumps,
			new PressureAlwaysPassableStrategy(),
			1.0
		);

		Assert.Multiple( () => {
			Assert.That( concentration[0, 0], Is.EqualTo( 2.0 ) );
			Assert.That( concentration[1, 0], Is.EqualTo( 0.0 ) );
		} );
	}

	[Test]
	public void Inject_ExtractionPump_ClampsAtZero() {
		TestGrid<double> concentration = new( 0, 0, 1, 1 );
		concentration.Set( 0, 0, 1.0 );

		GasSpecies<double, PressureScalarSetCellStrategy> gas = new( concentration, new PressureScalarSetCellStrategy( concentration ) );
		List<FixedRatePumpStrategy> pumps = [ new FixedRatePumpStrategy( 0, 0, -5.0 ) ];

		_pressure.Inject<double, double, FixedRatePumpStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
			concentration,
			gas,
			pumps,
			new PressureAlwaysPassableStrategy(),
			1.0
		);

		Assert.That( concentration[0, 0], Is.EqualTo( 0.0 ) );
	}

	[Test]
	public void Inject_SolidCell_SkipsPump() {
		TestGrid<double> concentration = new( 0, 0, 1, 1 );
		concentration.Set( 0, 0, 0.0 );

		GasSpecies<double, PressureScalarSetCellStrategy> gas = new( concentration, new PressureScalarSetCellStrategy( concentration ) );
		List<FixedRatePumpStrategy> pumps = [ new FixedRatePumpStrategy( 0, 0, 5.0 ) ];
		PressureWallPassableStrategy passability = new( [ ( 0, 0 ) ] );

		_pressure.Inject<double, double, FixedRatePumpStrategy, PressureWallPassableStrategy, PressureScalarSetCellStrategy>(
			concentration,
			gas,
			pumps,
			passability,
			1.0
		);

		Assert.That( concentration[0, 0], Is.EqualTo( 0.0 ) );
	}

	[Test]
	public void Inject_NullGrid_ThrowsArgumentNullException() {
		TestGrid<double> concentration = new( 0, 0, 1, 1 );
		GasSpecies<double, PressureScalarSetCellStrategy> gas = new( concentration, new PressureScalarSetCellStrategy( concentration ) );

		Assert.Throws<ArgumentNullException>( () => {
			_pressure.Inject<double, double, FixedRatePumpStrategy, PressureAlwaysPassableStrategy, PressureScalarSetCellStrategy>(
				null,
				gas,
				[],
				new PressureAlwaysPassableStrategy(),
				1.0
			);
		} );
	}

}
