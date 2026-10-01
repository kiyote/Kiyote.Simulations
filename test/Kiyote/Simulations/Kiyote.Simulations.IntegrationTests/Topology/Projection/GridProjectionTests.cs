using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Projection.IntegrationTests;

[TestFixture( 8 )]
[TestFixture( 16 )]
[ExcludeFromCodeCoverage]
public sealed class GridProjectionTests {

	private readonly IGridProjection _projection;
	private readonly int _chunkSize;

	public GridProjectionTests( int chunkSize ) {
		_chunkSize = chunkSize;
		_projection = new GridProjection( new Settings( 20 ) );
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
		Cells cells = new Cells( 20, 20 );
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
		using ICompiledGridAssembly<float> x = Compile( placements, p => p.X );
		using ICompiledGridAssembly<float> y = Compile( placements, p => p.Y );
		using ICompiledGridAssembly<float> pressure = Compile( placements, p => p.Pressure );

		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( x, strategy, 0 );
		ProjectionNeighbourhood neighbourhood = _projection.CreateNeighbourhood( x, connectivity );
		IGridLayer<float> vx = x.Bind<float, Identity>( default, 1 );
		IGridLayer<float> vxScratch = x.CreateLayer<float>( 1 );
		IGridLayer<float> vy = y.Bind<float, Identity>( default, 1 );
		IGridLayer<float> vyScratch = y.CreateLayer<float>( 1 );
		IGridLayer<float> p = pressure.Bind<float, Identity>( default, 1 );
		IGridLayer<float> pScratch = pressure.CreateLayer<float>( 1 );
		IGridLayer<float> divergence = pressure.CreateLayer<float>( 0 );

		for( int i = 0; i < steps; i++ ) {
			_projection.Update( neighbourhood, vx, vy, vxScratch, vyScratch, p, pScratch, divergence );
			x.Swap( vx, vxScratch );
			y.Swap( vy, vyScratch );
		}
		x.Commit();
		y.Commit();
		pressure.Commit();
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
