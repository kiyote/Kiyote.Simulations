using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Projection.IntegrationTests;

[TestFixture( 32 )]
[TestFixture( 64 )]
internal sealed class GridProjectionStorageTests {

	private readonly int _chunkSize;
	private DenseGridSource<Cell> _source;
	private ICompiledGridAssembly<Cell> _compiled;
	private ProjectionNeighbourhood _neighbourhood;
	private IGridLayer<float> _pressure;
	private IGridLayer<float> _pressureScratch;
	private IGridLayer<float> _velocityX;
	private IGridLayer<float> _velocityY;
	private IGridLayer<float> _outputX;
	private IGridLayer<float> _outputY;
	private IGridLayer<float> _divergence;
	private CommitCounter _commits;
	private int _emptyChunk;

	public GridProjectionStorageTests( int chunkSize ) {
		_chunkSize = chunkSize;
	}

	[SetUp]
	public void SetUp() {
		_source = new DenseGridSource<Cell>( ( 4 * _chunkSize ) + 2, 2 );
		Assert.That( _source.TrySetCell( 0, 0, new Cell( 8.0f, 17 ) ), Is.True );
		Assert.That( _source.TrySetCell( 1, 0, new Cell( 16.0f, 23 ) ), Is.True );
		Assert.That( _source.TrySetCell( ( 4 * _chunkSize ) + 1, 0, new Cell( 100.0f, 29 ) ), Is.True );
		IGridAssembly<Cell> assembly = new GridAssembly<Cell>();
		AttachResult attached = assembly.TryAttach( _source, -1, -_chunkSize );
		Assert.That( attached.Succeeded, Is.True );
		_compiled = new GridCompiler().Compile( assembly, _chunkSize );
		_commits = new CommitCounter();
		_pressure = _compiled.Bind<float, PressureBinding>( new PressureBinding( _commits ) );
		_pressureScratch = _compiled.CreateLayer<float>();
		_velocityX = _compiled.CreateLayer<float>();
		_velocityY = _compiled.CreateLayer<float>();
		_outputX = _compiled.CreateLayer<float>();
		_outputY = _compiled.CreateLayer<float>();
		_divergence = _compiled.CreateLayer<float>();
		Assert.That( _compiled.ChunkLayout.TryLocate( 4 * _chunkSize, -_chunkSize, out _emptyChunk, out _ ), Is.True );
		Assert.That( assembly.TryRemoveCell( attached.Placement, ( 4 * _chunkSize ) + 1, 0 ), Is.True );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open() );
		IGridProjection projection = new GridProjection( new Settings( 1 ) );
		_neighbourhood = projection.CreateNeighbourhood( _compiled, connectivity );
		_compiled.Commit();
		_commits.Writes = 0;
	}

	[TearDown]
	public void TearDown() {
		_compiled.Dispose();
	}

	[TestCase( 0, false )]
	[TestCase( 0, true )]
	[TestCase( 1, false )]
	[TestCase( 1, true )]
	[TestCase( 2, false )]
	[TestCase( 2, true )]
	[TestCase( 3, false )]
	[TestCase( 3, true )]
	[TestCase( 20, false )]
	[TestCase( 20, true )]
	[TestCase( 21, false )]
	[TestCase( 21, true )]
	public void Update_RepeatedWarmStarts_PreservesBoundPressureAndDirtyCommits( int iterations, bool commitEach ) {
		IGridProjection projection = new GridProjection( new Settings( iterations ) );
		float left = 8.0f;
		float right = 16.0f;
		for( int step = 0; step < 3; step++ ) {
			for( int i = 0; i < iterations; i++ ) {
				(left, right) = ( Flush( right / 8.0f ), Flush( left / 8.0f ) );
			}

			Update( projection );

			Assert.That( _pressure[-1, -_chunkSize], Is.EqualTo( left ) );
			Assert.That( _pressure[0, -_chunkSize], Is.EqualTo( right ) );
			Assert.That( _compiled.Layers, Does.Contain( _pressure ).And.Contain( _pressureScratch ) );
			Assert.That( _pressure.GetValues( _emptyChunk ).ToArray(), Is.All.Zero );
			if( commitEach ) {
				AssertCommit( left, right, iterations == 0 ? 0 : 2 );
			}
		}
		if( !commitEach ) {
			AssertCommit( left, right, iterations == 0 ? 0 : 2 );
		}
	}

	[TestCase( -1 )]
	[TestCase( -2 )]
	[TestCase( int.MinValue )]
	public void Constructor_NegativeIterations_ThrowsBeforeSolving( int iterations ) {
		Assert.Throws<ArgumentOutOfRangeException>( () => new GridProjection( new Settings( iterations ) ) );
	}

	[TestCase( 0 )]
	[TestCase( 1 )]
	[TestCase( 2 )]
	[TestCase( 21 )]
	public void Update_EmptyCompilation_PreservesEmptyStorage( int iterations ) {
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( new GridAssembly<float>(), _chunkSize );
		IGridProjection projection = new GridProjection( new Settings( iterations ) );
		ProjectionNeighbourhood neighbourhood = projection.CreateNeighbourhood( compiled, compiled.CreateLayer<Direction>() );
		IGridLayer<float> pressure = compiled.CreateLayer<float>();
		IGridLayer<float> scratch = compiled.CreateLayer<float>();

		projection.Update(
			compiled, neighbourhood,
			compiled.CreateLayer<float>(), compiled.CreateLayer<float>(),
			compiled.CreateLayer<float>(), compiled.CreateLayer<float>(),
			pressure, scratch, compiled.CreateLayer<float>()
		);
		compiled.Commit();

		Assert.That( compiled.ChunkLayout.IsEmpty, Is.True );
		Assert.That( compiled.Layers, Does.Contain( pressure ).And.Contain( scratch ) );
	}

	[Test]
	public void Update_OneIteration_SubtractsTheSolvedPressureGradient() {
		IGridProjection projection = new GridProjection( new Settings( 1 ) );

		Update( projection );

		Assert.That( _pressure[-1, -_chunkSize], Is.EqualTo( 2.0f ) );
		Assert.That( _pressure[0, -_chunkSize], Is.EqualTo( 1.0f ) );
		Assert.That( _outputX[-1, -_chunkSize], Is.EqualTo( -0.125f ).Within( 1e-6f ) );
		Assert.That( _outputX[0, -_chunkSize], Is.EqualTo( 0.25f ).Within( 1e-6f ) );
	}

	[TestCase( 1 )]
	[TestCase( 20 )]
	[TestCase( 21 )]
	public void Update_WarmedOddAndEvenIterations_AllocateNoManagedBytes( int iterations ) {
		IGridProjection projection = new GridProjection( new Settings( iterations ) );
		for( int i = 0; i < 100; i++ ) {
			Update( projection );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int i = 0; i < 100; i++ ) {
			Update( projection );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private void Update( IGridProjection projection ) {
		projection.Update( _compiled, _neighbourhood, _velocityX, _velocityY, _outputX, _outputY, _pressure, _pressureScratch, _divergence );
	}

	private void AssertCommit( float left, float right, int expectedWrites ) {
		_commits.Writes = 0;
		_compiled.Commit();
		Assert.That( _commits.Writes, Is.EqualTo( expectedWrites ) );
		Assert.That( _source.GetCell( 0, 0 ), Is.EqualTo( new Cell( left, 17 ) ) );
		Assert.That( _source.GetCell( 1, 0 ), Is.EqualTo( new Cell( right, 23 ) ) );
	}

	private static float Flush( float value ) => MathF.Abs( value ) >= 1.17549435E-38f ? value : 0.0f;

	private readonly record struct Cell( float Pressure, int Marker );

	private readonly record struct PressureBinding( CommitCounter Counter ) : IGridLayerBinding<Cell, float> {
		public float Extract( in Cell cell ) => cell.Pressure;
		public void Commit( ref Cell cell, float value ) {
			cell = cell with { Pressure = value };
			Counter.Writes++;
		}
	}

	private sealed class CommitCounter {
		public int Writes { get; set; }
	}

	private readonly struct Open : IConnectivityStrategy<Cell> {
		public bool Evaluate( in TopologyCell<Cell> source, in TopologyCell<Cell> destination, Direction direction, in TopologyCell<Cell> orthogonalA, in TopologyCell<Cell> orthogonalB, bool isSeam ) => destination.IsOccupied;
	}

	private sealed class Settings : IGridProjectionSettings {
		public Settings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}

}
