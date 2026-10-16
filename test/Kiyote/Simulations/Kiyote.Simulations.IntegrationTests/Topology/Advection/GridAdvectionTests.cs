using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;

namespace Kiyote.Simulations.Topology.Advection.IntegrationTests;

[TestFixture( 32 )]
[TestFixture( 64 )]
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
		Cells cells = new Cells( _chunkSize + 4, _chunkSize + 4 );
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

	[Test]
	public void Update_SpanningChunks_MatchesSingleChunk() {
		const int size = 40;
		// Offset so the grid straddles the corner where four chunks meet.
		int offset = _chunkSize - ( size / 2 );
		Cells spanning = CreatePattern( size );
		Cells single = CreatePattern( size );

		Step( new Box( offset + 12, offset + 12, offset + 27, offset + 27 ), (spanning, offset, offset) );
		Step( GridCompiler.MinimumChunkSize * 2, new Box( 12, 12, 27, 27 ), (single, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < size; row++ ) {
				for( int column = 0; column < size; column++ ) {
					if( single.Value.IsOccupied( column, row ) ) {
						Assert.That( spanning.Value.GetCell( column, row ), Is.EqualTo( single.Value.GetCell( column, row ) ).Within( 1e-4f ), $"({column},{row})" );
					}
				}
			}
		}
	}

	[Test]
	public void Update_DisplacementAcrossWholeChunk_CarriedFullDistance() {
		Cells cells = new Cells( ( _chunkSize * 2 ) + 8, 4 );
		cells.FillVelocity( ( _chunkSize + 2 ) * OneCell, 0f );
		cells.Value.GetCell( 3, 1 ) = 1000f;

		Step( new Open(), cells );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( cells.Value.GetCell( _chunkSize + 5, 1 ), Is.EqualTo( 1000f ).Within( 1e-2f ) );
			Assert.That( cells.Value.GetCell( 3, 1 ), Is.Zero );
		}
	}

	// A deterministic, uneven field with a few holes (vacuum). The top half moves less than a
	// cell per step (the vectorized path); the bottom half moves up to three cells (the walk).
	private Cells CreatePattern(
		int size
	) {
		Cells cells = new Cells( size, size );
		for( int row = 0; row < size; row++ ) {
			for( int column = 0; column < size; column++ ) {
				float scale = row < size / 2 ? 0.4f : 1.5f;
				cells.Value.GetCell( column, row ) = ( ( column * 37 ) + ( row * 53 ) ) % 101;
				cells.X.GetCell( column, row ) = ( ( ( ( column * 7 ) + ( row * 3 ) ) % 5 ) - 2 ) * scale * OneCell;
				cells.Y.GetCell( column, row ) = ( ( ( ( column * 3 ) + ( row * 11 ) ) % 5 ) - 2 ) * scale * OneCell;
			}
		}
		cells.Clear( 19, 5 );
		cells.Clear( 20, 20 );
		cells.Clear( 33, 19 );
		return cells;
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
		Step( _chunkSize, strategy, placements );
	}

	private void Step<TStrategy>(
		int chunkSize,
		TStrategy strategy,
		params (Cells Cells, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		using ICompiledGridAssembly<float> value = Compile( chunkSize, placements, p => p.Value );
		using ICompiledGridAssembly<float> x = Compile( chunkSize, placements, p => p.X );
		using ICompiledGridAssembly<float> y = Compile( chunkSize, placements, p => p.Y );

		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( value, strategy );
		AdvectionNeighbourhood neighbourhood = _advection.CreateNeighbourhood( value, connectivity );
		IGridLayer<float> source = value.Bind<float, Identity>( default );
		IGridLayer<float> destination = value.CreateLayer<float>();
		IGridLayer<float> vx = x.Bind<float, Identity>( default );
		IGridLayer<float> vy = y.Bind<float, Identity>( default );

		_advection.Update( neighbourhood, vx, vy, source, destination );
		value.Swap( source, destination );
		value.Commit();
	}

	private static ICompiledGridAssembly<float> Compile(
		int chunkSize,
		(Cells Cells, int Column, int Row)[] placements,
		Func<Cells, DenseGridSource<float>> select
	) {
		IGridAssembly<float> assembly = new GridAssembly<float>();
		foreach( (Cells cells, int column, int row) in placements ) {
			Assert.That( assembly.TryAttach( select( cells ), column, row ).Succeeded, Is.True );
		}
		return new GridCompiler().Compile( assembly, chunkSize );
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

		public void Clear(
			int column,
			int row
		) {
			Value.TryClearCell( column, row );
			X.TryClearCell( column, row );
			Y.TryClearCell( column, row );
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
