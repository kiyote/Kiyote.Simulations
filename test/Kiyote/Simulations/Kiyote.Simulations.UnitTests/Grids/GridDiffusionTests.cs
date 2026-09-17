using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

[ExcludeFromCodeCoverage]
internal readonly struct IntEqualizationFlowStrategy : IDiffusionStrategy<int, int> {

	private readonly double _viscosity;

	public IntEqualizationFlowStrategy() : this( 1.0 ) { }

	// viscosity ranges from 0 (no flow) to 1 (fully equalizes in a single step).
	public IntEqualizationFlowStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public int CalculateTransfer(
		GridCell<int> source,
		GridCell<int> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return (int)( ( source.Cell - destination.Cell ) * _viscosity / divisor );
	}

	public int Combine(
		int left,
		int right
	) {
		return left + right;
	}

	public int Negate(
		int value
	) {
		return -value;
	}

	public int Apply(
		GridCell<int> cell,
		int delta
	) {
		return cell.Cell + delta;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct AlwaysPassableStrategy : ICellStrategy<int, bool> {

	public bool Evaluate(
		GridCell<int> cell
	) {
		return true;
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct WallPassableStrategy : ICellStrategy<int, bool> {

	private readonly HashSet<(int Column, int Row)> _walls;

	public WallPassableStrategy(
		HashSet<(int Column, int Row)> walls
	) {
		_walls = walls;
	}

	public bool Evaluate(
		GridCell<int> cell
	) {
		return !_walls.Contains( ( cell.Column, cell.Row ) );
	}

}

[ExcludeFromCodeCoverage]
internal readonly struct TestGridSetCellStrategy : ICallbackStrategy<int, int> {

	private readonly TestGrid<int> _grid;

	public TestGridSetCellStrategy(
		TestGrid<int> grid
	) {
		_grid = grid;
	}

	public void Callback(
		GridCell<int> cell,
		int value
	) {
		_grid.Set( cell.Column, cell.Row, value );
	}

}

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridDiffusionTests {

	private IGridDiffusion _flow;

	[SetUp]
	public void SetUp() {
		_flow = new GridDiffusion();
	}

	private static TestGrid<int> CreateGrid(
		int[][] values
	) {
		int height = values.Length;
		int width = values[0].Length;
		TestGrid<int> grid = new( 0, 0, width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.Set( column, row, values[row][column] );
			}
		}
		return grid;
	}

	[Test]
	public void Flow_UnevenNeighbors_EqualizesValues() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0 ],
		] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 1.0 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		Assert.Multiple( () => {
			Assert.That( grid[0, 0], Is.EqualTo( 5 ) );
			Assert.That( grid[1, 0], Is.EqualTo( 5 ) );
		} );
	}

	[Test]
	public void Flow_EqualNeighbors_NoChange() {
		TestGrid<int> grid = CreateGrid( [
			[ 5, 5 ],
			[ 5, 5 ],
		] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 1.0 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		Assert.Multiple( () => {
			Assert.That( grid[0, 0], Is.EqualTo( 5 ) );
			Assert.That( grid[1, 0], Is.EqualTo( 5 ) );
			Assert.That( grid[0, 1], Is.EqualTo( 5 ) );
			Assert.That( grid[1, 1], Is.EqualTo( 5 ) );
		} );
	}

	[Test]
	public void Flow_DiagonalNeighbor_Equalizes() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0 ],
			[ 0, 0 ],
		] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 1.0 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		Assert.That( grid[1, 1], Is.Not.EqualTo( 0 ) );
	}

	[Test]
	public void Flow_ImpassableDestination_BlocksTransfer() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0 ],
		] );

		WallPassableStrategy isPassable = new( [ ( 1, 0 ) ] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, WallPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 1.0 ),
			isPassable,
			new TestGridSetCellStrategy( grid )
		);

		Assert.Multiple( () => {
			Assert.That( grid[0, 0], Is.EqualTo( 10 ) );
			Assert.That( grid[1, 0], Is.EqualTo( 0 ) );
		} );
	}

	[Test]
	public void Flow_LowViscosity_PartiallyFlows() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0 ],
		] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 0.2 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		Assert.Multiple( () => {
			Assert.That( grid[0, 0], Is.EqualTo( 9 ) );
			Assert.That( grid[1, 0], Is.EqualTo( 1 ) );
		} );
	}

	[Test]
	public void Flow_ZeroViscosity_NoFlow() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0 ],
		] );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 0.0 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		Assert.Multiple( () => {
			Assert.That( grid[0, 0], Is.EqualTo( 10 ) );
			Assert.That( grid[1, 0], Is.EqualTo( 0 ) );
		} );
	}

	[Test]
	public void Flow_ConservesTotalQuantity() {
		TestGrid<int> grid = CreateGrid( [
			[ 10, 0, 4 ],
			[ 2, 8, 6 ],
		] );

		static int Total(
			TestGrid<int> g
		) {
			int total = 0;
			for( int row = 0; row < 2; row++ ) {
				for( int column = 0; column < 3; column++ ) {
					total += g[column, row];
				}
			}
			return total;
		}

		int before = Total( grid );

		_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
			grid,
			new IntEqualizationFlowStrategy( 1.0 ),
			new AlwaysPassableStrategy(),
			new TestGridSetCellStrategy( grid )
		);

		int after = Total( grid );

		Assert.That( after, Is.EqualTo( before ) );
	}

	[Test]
	public void Flow_NullGrid_ThrowsArgumentNullException() {
		Assert.Throws<ArgumentNullException>( () => {
			_flow.Flow<int, int, IntEqualizationFlowStrategy, AlwaysPassableStrategy, TestGridSetCellStrategy>(
				null,
				new IntEqualizationFlowStrategy( 1.0 ),
				new AlwaysPassableStrategy(),
				default
			);
		} );
	}

}
