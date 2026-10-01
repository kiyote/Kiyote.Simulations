using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Pressure;

namespace Kiyote.Simulations.Topology.Pressure.IntegrationTests;

[TestFixture( 8 )]
[TestFixture( 16 )]
[ExcludeFromCodeCoverage]
public sealed class GridPressureTests {

	private const float Rate = 1f;

	private readonly ISimulationClock _clock;
	private readonly IGridPressure _pressure;
	private readonly int _chunkSize;

	public GridPressureTests( int chunkSize ) {
		_chunkSize = chunkSize;
		_clock = new SimulationClock();
		_pressure = new GridPressure( new Settings( Rate ), _clock );
	}

	[Test]
	public void Update_OneStep_TransferScaledByTimeStep() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 5, 5 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		float transfer = Rate * _clock.FixedTimeStep * 1000f;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( grid.GetCell( 5, 5 ), Is.EqualTo( 1000f - ( 8 * transfer ) ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 4, 4 ), Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 5, 4 ), Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 6, 6 ), Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 3, 5 ), Is.Zero );
		}
	}

	[Test]
	public void Update_WalledBox_TotalConserved() {
		DenseGridSource<float> grid = Create( 20, 20 );
		grid.GetCell( 2, 2 ) = 1000f;
		grid.GetCell( 15, 12 ) = 500f;

		Step( new Box( 1, 1, 18, 18 ), 200, (grid, 0, 0) );

		Assert.That( Sum( grid ), Is.EqualTo( 1500f ).Within( 0.05f ) );
	}

	[Test]
	public void Update_WalledBox_Equalises() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 5, 5 ) = 1000f;

		// The 8x8 interior holds 64 cells, so the level settles at 1000 / 64.
		Step( new Box( 1, 1, 8, 8 ), 2000, (grid, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( grid.GetCell( 1, 1 ), Is.EqualTo( 1000f / 64f ).Within( 0.5f ) );
			Assert.That( grid.GetCell( 8, 8 ), Is.EqualTo( 1000f / 64f ).Within( 0.5f ) );
			Assert.That( grid.GetCell( 5, 5 ), Is.EqualTo( 1000f / 64f ).Within( 0.5f ) );
			Assert.That( grid.GetCell( 0, 0 ), Is.Zero );
		}
	}

	[Test]
	public void Update_GridEdge_LosesToVacuum() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 0, 5 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		float transfer = Rate * _clock.FixedTimeStep * 1000f;
		Assert.That( Sum( grid ), Is.EqualTo( 1000f - ( 3 * transfer ) ).Within( 0.001f ) );
	}

	[Test]
	public void Update_AcrossChunkBoundary_SpreadsThroughHalo() {
		DenseGridSource<float> grid = Create( 20, 20 );
		grid.GetCell( _chunkSize - 1, _chunkSize - 1 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		float transfer = Rate * _clock.FixedTimeStep * 1000f;
		Assert.That( grid.GetCell( _chunkSize, _chunkSize ), Is.EqualTo( transfer ).Within( 0.001f ) );
	}

	[Test]
	public void Update_TwoPlacements_FlowsAcrossSeam() {
		DenseGridSource<float> left = Create( 5, 5 );
		DenseGridSource<float> right = Create( 5, 5 );
		left.GetCell( 4, 2 ) = 1000f;

		Step( new Open(), 1, (left, 0, 0), (right, 5, 0) );

		float transfer = Rate * _clock.FixedTimeStep * 1000f;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( right.GetCell( 0, 2 ), Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( Sum( left ) + Sum( right ), Is.EqualTo( 1000f ).Within( 0.001f ) );
		}
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		int steps,
		params (DenseGridSource<float> Grid, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		IGridAssembly<float> assembly = new GridAssembly<float>();
		foreach( (DenseGridSource<float> grid, int column, int row) in placements ) {
			Assert.That( assembly.TryAttach( grid, column, row ).Succeeded, Is.True );
		}
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, _chunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( compiled, strategy, 0 );
		IGridLayer<Direction> neighbourhood = _pressure.CreateNeighbourhood( compiled, connectivity );
		IGridLayer<float> values = compiled.Bind<float, Identity>( default, 1 );
		IGridLayer<float> scratch = compiled.CreateLayer<float>( 1 );
		for( int i = 0; i < steps; i++ ) {
			_pressure.Update( neighbourhood, values, scratch );
			compiled.Swap( values, scratch );
		}
		compiled.Commit();
	}

	private static DenseGridSource<float> Create(
		int width,
		int height
	) {
		DenseGridSource<float> grid = new DenseGridSource<float>( width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.TrySetCell( column, row, 0f );
			}
		}
		return grid;
	}

	private static float Sum(
		DenseGridSource<float> grid
	) {
		float total = 0f;
		for( int row = 0; row < grid.Height; row++ ) {
			for( int column = 0; column < grid.Width; column++ ) {
				if( grid.IsOccupied( column, row ) ) {
					total += grid.GetCell( column, row );
				}
			}
		}
		return total;
	}

	private readonly struct Identity : IGridLayerBinding<float, float> {
		float IGridLayerBinding<float, float>.Extract( in float cell ) => cell;
		void IGridLayerBinding<float, float>.Commit( ref float cell, float value ) => cell = value;
	}

	private readonly struct Open : IConnectivityStrategy<float> {
		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied;
	}

	private readonly struct Box : IConnectivityStrategy<float> {
		private readonly int _left;
		private readonly int _top;
		private readonly int _right;
		private readonly int _bottom;

		public Box( int left, int top, int right, int bottom ) {
			_left = left;
			_top = top;
			_right = right;
			_bottom = bottom;
		}

		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied && Inside( source ) == Inside( destination );

		private bool Inside( in TopologyCell<float> cell ) =>
			cell.Column >= _left && cell.Column <= _right && cell.Row >= _top && cell.Row <= _bottom;
	}

	private sealed class Settings : IGridPressureSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

}
