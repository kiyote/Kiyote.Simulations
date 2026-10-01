using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;

namespace Kiyote.Simulations.Topology.Advection.IntegrationTests;

[TestFixture( 8 )]
[TestFixture( 16 )]
[ExcludeFromCodeCoverage]
public sealed class GridAdvectionTests {

	private readonly ISimulationClock _clock;
	private readonly IGridAdvection _advection;
	private readonly int _chunkSize;

	public GridAdvectionTests( int chunkSize ) {
		_chunkSize = chunkSize;
		_clock = new SimulationClock();
		_advection = new GridAdvection( _clock );
	}

	// One cell per step along +X.
	private float OneCell => 1f / _clock.FixedTimeStep;

	[Test]
	public void Update_UniformWind_ShiftsOneCell() {
		Cells cells = new Cells( 10, 10 );
		cells.FillVelocity( OneCell, 0f );
		cells.Value.GetCell( 3, 5 ) = 1000f;
		cells.Value.GetCell( 6, 2 ) = 500f;

		Step( new Open(), cells );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < 10; row++ ) {
				for( int column = 0; column < 10; column++ ) {
					float expected = ( column, row ) switch {
						(4, 5) => 1000f,
						(7, 2) => 500f,
						_ => 0f
					};
					Assert.That( cells.Value.GetCell( column, row ), Is.EqualTo( expected ).Within( 1e-4f ), $"({column},{row})" );
				}
			}
		}
	}

	[Test]
	public void Update_MultiCellDisplacement_CarriedFullDistance() {
		Cells cells = new Cells( 20, 20 );
		cells.FillVelocity( 3f * OneCell, 0f );
		cells.Value.GetCell( 5, 5 ) = 1000f;

		Step( new Open(), cells );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( cells.Value.GetCell( 8, 5 ), Is.EqualTo( 1000f ).Within( 1e-3f ) );
			Assert.That( cells.Value.GetCell( 5, 5 ), Is.Zero );
		}
	}

	[Test]
	public void Update_HalfCell_Interpolates() {
		Cells cells = new Cells( 10, 10 );
		cells.FillVelocity( 0.5f * OneCell, 0f );
		cells.Value.GetCell( 4, 5 ) = 1000f;

		Step( new Open(), cells );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( cells.Value.GetCell( 4, 5 ), Is.EqualTo( 500f ).Within( 1e-2f ) );
			Assert.That( cells.Value.GetCell( 5, 5 ), Is.EqualTo( 500f ).Within( 1e-2f ) );
		}
	}

	[Test]
	public void Update_AcrossChunkBoundary_CarriesValue() {
		Cells cells = new Cells( 20, 20 );
		cells.FillVelocity( 2f * OneCell, OneCell );
		cells.Value.GetCell( _chunkSize - 1, _chunkSize - 1 ) = 1000f;

		Step( new Open(), cells );

		Assert.That( cells.Value.GetCell( _chunkSize + 1, _chunkSize ), Is.EqualTo( 1000f ).Within( 1e-3f ) );
	}

	[Test]
	public void Update_GridEdge_InflowFromVacuumIsZero() {
		Cells cells = new Cells( 10, 10 );
		cells.FillVelocity( OneCell, 0f );
		for( int row = 0; row < 10; row++ ) {
			for( int column = 0; column < 10; column++ ) {
				cells.Value.GetCell( column, row ) = 100f;
			}
		}

		Step( new Open(), cells );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( cells.Value.GetCell( 0, 5 ), Is.Zero );
			Assert.That( cells.Value.GetCell( 1, 5 ), Is.EqualTo( 100f ).Within( 1e-4f ) );
		}
	}

	[Test]
	public void Update_Wall_StopsWalk() {
		Cells cells = new Cells( 10, 10 );
		cells.FillVelocity( OneCell, 0f );
		cells.Value.GetCell( 2, 5 ) = 1000f;
		cells.Value.GetCell( 4, 5 ) = 50f;

		Step( new Box( 3, 3, 7, 7 ), cells );

		using( Assert.EnterMultipleScope() ) {
			// (3,5) is inside the box; its backtrace hits the wall and stays put.
			Assert.That( cells.Value.GetCell( 3, 5 ), Is.Zero );
			Assert.That( cells.Value.GetCell( 5, 5 ), Is.EqualTo( 50f ).Within( 1e-4f ) );
		}
	}

	[Test]
	public void Update_TwoPlacements_FlowsAcrossSeam() {
		Cells left = new Cells( 5, 10 );
		Cells right = new Cells( 5, 10 );
		left.FillVelocity( OneCell, 0f );
		right.FillVelocity( OneCell, 0f );
		left.Value.GetCell( 4, 5 ) = 1000f;

		Step( new Open(), (left, 0, 0), (right, 5, 0) );

		Assert.That( right.Value.GetCell( 0, 5 ), Is.EqualTo( 1000f ).Within( 1e-4f ) );
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		Cells cells
	) where TStrategy : struct, IConnectivityStrategy<float> {
		Step( strategy, (cells, 0, 0) );
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		params (Cells Cells, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		using ICompiledGridAssembly<float> value = Compile( placements, p => p.Value );
		using ICompiledGridAssembly<float> x = Compile( placements, p => p.X );
		using ICompiledGridAssembly<float> y = Compile( placements, p => p.Y );

		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( value, strategy, 0 );
		AdvectionNeighbourhood neighbourhood = _advection.CreateNeighbourhood( value, connectivity );
		IGridLayer<float> source = value.Bind<float, Identity>( default, 1 );
		IGridLayer<float> destination = value.CreateLayer<float>( 1 );
		IGridLayer<float> vx = x.Bind<float, Identity>( default, 0 );
		IGridLayer<float> vy = y.Bind<float, Identity>( default, 0 );

		_advection.Update( neighbourhood, vx, vy, source, destination );
		value.Swap( source, destination );
		value.Commit();
	}

	private ICompiledGridAssembly<float> Compile(
		(Cells Cells, int Column, int Row)[] placements,
		Func<Cells, DenseGridSource<float>> select
	) {
		IGridAssembly<float> assembly = new GridAssembly<float>();
		foreach( (Cells cells, int column, int row) in placements ) {
			Assert.That( assembly.TryAttach( select( cells ), column, row ).Succeeded, Is.True );
		}
		return new GridCompiler().Compile( assembly, _chunkSize );
	}

	private sealed class Cells {

		public Cells(
			int width,
			int height
		) {
			Value = Create( width, height );
			X = Create( width, height );
			Y = Create( width, height );
		}

		public DenseGridSource<float> Value { get; }
		public DenseGridSource<float> X { get; }
		public DenseGridSource<float> Y { get; }

		public void FillVelocity(
			float x,
			float y
		) {
			for( int row = 0; row < X.Height; row++ ) {
				for( int column = 0; column < X.Width; column++ ) {
					X.GetCell( column, row ) = x;
					Y.GetCell( column, row ) = y;
				}
			}
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

}
