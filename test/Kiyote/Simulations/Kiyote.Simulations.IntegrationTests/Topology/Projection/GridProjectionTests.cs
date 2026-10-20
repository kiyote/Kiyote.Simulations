using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Projection.IntegrationTests;

[TestFixture( 32, 20 )]
[TestFixture( 32, 21 )]
[TestFixture( 64, 20 )]
[TestFixture( 64, 21 )]
[ExcludeFromCodeCoverage]
public sealed class GridProjectionTests {

	private readonly IGridProjection _projection;
	private readonly int _chunkSize;

	public GridProjectionTests( int chunkSize, int iterations ) {
		_chunkSize = chunkSize;
		_projection = new GridProjection( new Settings( iterations ) );
	}

	[Test]
	public void Update_ZeroVelocity_RemainsZero() {
		Cells cells = new Cells( 10, 10 );

		Step( new Open(), 1, cells );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( cells.MaxSpeed(), Is.Zero );
			Assert.That( cells.MaxPressure(), Is.Zero );
		}
	}

	[Test]
	public void Update_Outflow_OpposingPressureBuildsAndReducesDivergence() {
		Cells cells = new Cells( 10, 10 );
		cells.X.GetCell( 4, 5 ) = -5f;
		cells.X.GetCell( 6, 5 ) = 5f;

		Step( new Open(), 1, cells );

		using( Assert.EnterMultipleScope() ) {
			// Air leaving the centre leaves a low pressure behind it, which pulls it back.
			Assert.That( cells.Pressure.GetCell( 5, 5 ), Is.LessThan( 0f ) );
			Assert.That( cells.X.GetCell( 4, 5 ), Is.GreaterThan( -5f ) );
			Assert.That( cells.X.GetCell( 6, 5 ), Is.LessThan( 5f ) );
		}
	}

	[Test]
	public void Update_RepeatedCalls_VelocityRemainsBounded() {
		Cells cells = new Cells( 20, 20 );
		cells.X.GetCell( 5, 5 ) = 5f;
		cells.Y.GetCell( 12, 2 ) = -10f;

		Step( new Open(), 200, cells );

		float max = cells.MaxSpeed();
		Assert.That( float.IsFinite( max ) && max <= 10f, Is.True, $"max={max}" );
	}

	[Test]
	public void Update_WalledBox_OutsideUnchanged() {
		Cells cells = new Cells( 10, 10 );
		cells.X.GetCell( 5, 5 ) = 5f;
		cells.X.GetCell( 1, 1 ) = 7f;

		Step( new Box( 3, 3, 7, 7 ), 5, cells );

		using( Assert.EnterMultipleScope() ) {
			// (1,1) sits in the open region outside the box, with no divergence around it except its own.
			Assert.That( cells.Pressure.GetCell( 8, 8 ), Is.Zero.Within( 1e-3f ) );
			Assert.That( cells.X.GetCell( 9, 9 ), Is.Zero.Within( 1e-3f ) );
			Assert.That( cells.Pressure.GetCell( 5, 5 ), Is.Not.Zero );
		}
	}

	[Test]
	public void Update_AcrossChunkBoundary_PressureReachesNeighbourChunk() {
		Cells cells = new Cells( _chunkSize + 4, _chunkSize + 4 );
		cells.X.GetCell( _chunkSize - 1, _chunkSize - 1 ) = 5f;

		Step( new Open(), 1, cells );

		Assert.That( cells.Pressure.GetCell( _chunkSize, _chunkSize - 1 ), Is.Not.Zero );
	}

	[Test]
	public void Update_TwoPlacements_MatchesSinglePlacement() {
		Cells whole = new Cells( 10, 5 );
		whole.X.GetCell( 4, 2 ) = 5f;
		Step( new Open(), 3, whole );

		Cells left = new Cells( 5, 5 );
		Cells right = new Cells( 5, 5 );
		left.X.GetCell( 4, 2 ) = 5f;
		Step( new Open(), 3, (left, 0, 0), (right, 5, 0) );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < 5; row++ ) {
				for( int column = 0; column < 10; column++ ) {
					Cells part = column < 5 ? left : right;
					int c = column % 5;
					Assert.That( part.X.GetCell( c, row ), Is.EqualTo( whole.X.GetCell( column, row ) ).Within( 1e-4f ), $"X ({column},{row})" );
					Assert.That( part.Y.GetCell( c, row ), Is.EqualTo( whole.Y.GetCell( column, row ) ).Within( 1e-4f ), $"Y ({column},{row})" );
				}
			}
		}
	}

	[Test]
	public void Update_SpanningChunks_MatchesSingleChunk() {
		const int size = 40;
		// Offset so the grid straddles the corner where four chunks meet.
		int offset = _chunkSize - ( size / 2 );
		Cells spanning = CreatePattern( size );
		Cells single = CreatePattern( size );

		Step( new Box( offset + 12, offset + 12, offset + 27, offset + 27 ), 3, (spanning, offset, offset) );
		Step( GridCompiler.MinimumChunkSize * 2, new Box( 12, 12, 27, 27 ), 3, (single, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < size; row++ ) {
				for( int column = 0; column < size; column++ ) {
					Assert.That( spanning.X.GetCell( column, row ), Is.EqualTo( single.X.GetCell( column, row ) ).Within( 1e-4f ), $"X ({column},{row})" );
					Assert.That( spanning.Y.GetCell( column, row ), Is.EqualTo( single.Y.GetCell( column, row ) ).Within( 1e-4f ), $"Y ({column},{row})" );
					Assert.That( spanning.Pressure.GetCell( column, row ), Is.EqualTo( single.Pressure.GetCell( column, row ) ).Within( 1e-4f ), $"P ({column},{row})" );
				}
			}
		}
	}

	// A deterministic, uneven velocity field.
	private static Cells CreatePattern(
		int size
	) {
		Cells cells = new Cells( size, size );
		for( int row = 0; row < size; row++ ) {
			for( int column = 0; column < size; column++ ) {
				cells.X.GetCell( column, row ) = ( ( ( column * 37 ) + ( row * 53 ) ) % 11 ) - 5f;
				cells.Y.GetCell( column, row ) = ( ( ( column * 29 ) + ( row * 17 ) ) % 13 ) - 6f;
			}
		}
		return cells;
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		int steps,
		Cells cells
	) where TStrategy : struct, IConnectivityStrategy<float> {
		Step( strategy, steps, (cells, 0, 0) );
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		int steps,
		params (Cells Cells, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		Step( _chunkSize, strategy, steps, placements );
	}

	private void Step<TStrategy>(
		int chunkSize,
		TStrategy strategy,
		int steps,
		params (Cells Cells, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		using ICompiledGridAssembly<float> x = Compile( chunkSize, placements, p => p.X );
		using ICompiledGridAssembly<float> y = Compile( chunkSize, placements, p => p.Y );
		using ICompiledGridAssembly<float> pressure = Compile( chunkSize, placements, p => p.Pressure );

		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( x, strategy );
		ProjectionNeighbourhood neighbourhood = _projection.CreateNeighbourhood( x, connectivity );
		IGridLayer<float> vx = x.Bind<float, Identity>( default );
		IGridLayer<float> vxScratch = x.CreateLayer<float>();
		IGridLayer<float> vy = y.Bind<float, Identity>( default );
		IGridLayer<float> vyScratch = y.CreateLayer<float>();
		IGridLayer<float> p = pressure.Bind<float, Identity>( default );
		IGridLayer<float> pScratch = pressure.CreateLayer<float>();
		IGridLayer<float> divergence = pressure.CreateLayer<float>();

		for( int i = 0; i < steps; i++ ) {
			_projection.Update( neighbourhood, vx, vy, vxScratch, vyScratch, p, pScratch, divergence );
			x.Swap( vx, vxScratch );
			y.Swap( vy, vyScratch );
		}
		x.Commit();
		y.Commit();
		pressure.Commit();
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
			X = Create( width, height );
			Y = Create( width, height );
			Pressure = Create( width, height );
		}

		public DenseGridSource<float> X { get; }
		public DenseGridSource<float> Y { get; }
		public DenseGridSource<float> Pressure { get; }

		public float MaxSpeed() {
			float max = 0f;
			for( int row = 0; row < X.Height; row++ ) {
				for( int column = 0; column < X.Width; column++ ) {
					max = MathF.Max( max, MathF.Abs( X.GetCell( column, row ) ) );
					max = MathF.Max( max, MathF.Abs( Y.GetCell( column, row ) ) );
				}
			}
			return max;
		}

		public float MaxPressure() {
			float max = 0f;
			for( int row = 0; row < Pressure.Height; row++ ) {
				for( int column = 0; column < Pressure.Width; column++ ) {
					max = MathF.Max( max, MathF.Abs( Pressure.GetCell( column, row ) ) );
				}
			}
			return max;
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

	private sealed class Settings : IGridProjectionSettings {
		public Settings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}

}
