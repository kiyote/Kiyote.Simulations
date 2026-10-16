using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Diffusion.IntegrationTests;

[TestFixture( 32 )]
[TestFixture( 64 )]
[ExcludeFromCodeCoverage]
public sealed class GridDiffusionTests {

	private const float Rate = 0.1f;

	private readonly IGridDiffusion _diffusion;
	private readonly int ChunkSize;

	public GridDiffusionTests( int chunkSize ) {
		ChunkSize = chunkSize;
		_diffusion = new GridDiffusion( new Settings( Rate ) );
	}

	[Test]
	public void Update_OneStep_SpreadsToOpenNeighbours() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 5, 5 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( grid.GetCell( 5, 5 ), Is.EqualTo( 1000f - ( 8 * Rate * 1000f ) ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 4, 4 ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 6, 5 ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 3, 3 ), Is.Zero );
		}
	}

	[Test]
	public void Update_AcrossChunkBoundary_SpreadsToNeighbourChunks() {
		DenseGridSource<float> grid = Create( ChunkSize + 4, ChunkSize + 4 );
		grid.GetCell( ChunkSize - 1, ChunkSize - 1 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( grid.GetCell( ChunkSize, ChunkSize ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( grid.GetCell( ChunkSize, ChunkSize - 1 ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( grid.GetCell( ChunkSize - 1, ChunkSize ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
		}
	}

	[Test]
	public void Update_GridEdge_LosesToVacuum() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 0, 5 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		// Three of the eight directions are off the grid.
		Assert.That( Sum( grid ), Is.EqualTo( 1000f - ( 3 * Rate * 1000f ) ).Within( 0.001f ) );
	}

	[Test]
	public void Update_HoleInGrid_LosesToVacuum() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.TryClearCell( 6, 5 );
		grid.GetCell( 5, 5 ) = 1000f;

		Step( new Open(), 1, (grid, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( Sum( grid ), Is.EqualTo( 1000f - ( Rate * 1000f ) ).Within( 0.001f ) );
			Assert.That( grid.GetCell( 6, 5 ), Is.Zero );
		}
	}

	[Test]
	public void Update_WalledBox_ConservesAndStaysInside() {
		DenseGridSource<float> grid = Create( 10, 10 );
		grid.GetCell( 5, 5 ) = 1000f;

		Step( new Box( 4, 4, 6, 6 ), 200, (grid, 0, 0) );

		float inside = 0f;
		for( int row = 4; row <= 6; row++ ) {
			for( int column = 4; column <= 6; column++ ) {
				inside += grid.GetCell( column, row );
			}
		}
		using( Assert.EnterMultipleScope() ) {
			Assert.That( inside, Is.EqualTo( 1000f ).Within( 0.05f ) );
			Assert.That( grid.GetCell( 4, 4 ), Is.EqualTo( 1000f / 9f ).Within( 1f ) );
			Assert.That( grid.GetCell( 2, 2 ), Is.Zero );
		}
	}

	[Test]
	public void Update_TwoPlacements_FlowsAcrossSeam() {
		DenseGridSource<float> left = Create( 5, 5 );
		DenseGridSource<float> right = Create( 5, 5 );
		left.GetCell( 4, 2 ) = 1000f;

		Step( new Open(), 1, (left, 0, 0), (right, 5, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( right.GetCell( 0, 2 ), Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( Sum( left ) + Sum( right ), Is.EqualTo( 1000f ).Within( 0.001f ) );
		}
	}

	[Test]
	public void Update_SpanningChunks_MatchesSingleChunk() {
		const int width = 40;
		const int height = 40;
		// Offset so the grid straddles the corner where four chunks meet.
		int offset = ChunkSize - ( width / 2 );
		DenseGridSource<float> spanning = CreatePattern( width, height );
		DenseGridSource<float> single = CreatePattern( width, height );

		Step( new Box( offset + 12, offset + 12, offset + 27, offset + 27 ), 25, (spanning, offset, offset) );
		Step( GridCompiler.MinimumChunkSize * 2, new Box( 12, 12, 27, 27 ), 25, (single, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < height; row++ ) {
				for( int column = 0; column < width; column++ ) {
					if( single.IsOccupied( column, row ) ) {
						Assert.That( spanning.GetCell( column, row ), Is.EqualTo( single.GetCell( column, row ) ).Within( 1e-4f ), $"({column},{row})" );
					}
				}
			}
			Assert.That( Sum( spanning ), Is.EqualTo( Sum( single ) ).Within( 0.01f ) );
		}
	}

	[Test]
	public void Update_WalledBoxAcrossChunkCorner_Conserves() {
		int size = ChunkSize * 2;
		DenseGridSource<float> grid = Create( size, size );
		grid.GetCell( ChunkSize - 1, ChunkSize - 1 ) = 1000f;

		Step( new Box( ChunkSize - 4, ChunkSize - 4, ChunkSize + 3, ChunkSize + 3 ), 200, (grid, 0, 0) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( Sum( grid ), Is.EqualTo( 1000f ).Within( 0.05f ) );
			Assert.That( grid.GetCell( ChunkSize - 4, ChunkSize - 4 ), Is.EqualTo( grid.GetCell( ChunkSize + 3, ChunkSize + 3 ) ).Within( 0.5f ) );
			Assert.That( grid.GetCell( ChunkSize - 4, ChunkSize + 3 ), Is.EqualTo( grid.GetCell( ChunkSize + 3, ChunkSize - 4 ) ).Within( 0.5f ) );
		}
	}

	private void Step<TStrategy>(
		TStrategy strategy,
		int steps,
		params (DenseGridSource<float> Grid, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		Step( ChunkSize, strategy, steps, placements );
	}

	private void Step<TStrategy>(
		int chunkSize,
		TStrategy strategy,
		int steps,
		params (DenseGridSource<float> Grid, int Column, int Row)[] placements
	) where TStrategy : struct, IConnectivityStrategy<float> {
		IGridAssembly<float> assembly = new GridAssembly<float>();
		foreach( (DenseGridSource<float> grid, int column, int row) in placements ) {
			Assert.That( assembly.TryAttach( grid, column, row ).Succeeded, Is.True );
		}
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, chunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( compiled, strategy );
		IGridLayer<Direction> neighbourhood = _diffusion.CreateNeighbourhood( compiled, connectivity );
		IGridLayer<float> values = compiled.Bind<float, Identity>( default );
		IGridLayer<float> scratch = compiled.CreateLayer<float>();
		for( int i = 0; i < steps; i++ ) {
			_diffusion.Update( neighbourhood, values, scratch );
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

	// A deterministic, uneven field with a few holes (vacuum) scattered through it.
	private static DenseGridSource<float> CreatePattern(
		int width,
		int height
	) {
		DenseGridSource<float> grid = Create( width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				grid.GetCell( column, row ) = ( ( column * 37 ) + ( row * 53 ) ) % 101;
			}
		}
		grid.TryClearCell( 19, 5 );
		grid.TryClearCell( 20, 20 );
		grid.TryClearCell( 33, 19 );
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

	private sealed class Settings : IGridDiffusionSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

}
