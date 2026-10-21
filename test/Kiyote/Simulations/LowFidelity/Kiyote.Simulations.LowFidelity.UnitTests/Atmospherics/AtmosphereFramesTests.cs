using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using Moq;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereFramesTests {

	// Wide enough to span two chunks.
	private const int Width = AtmosphereSimd.ChunkSize + 8;
	private const int Height = 4;
	private static readonly int[] SampleColumns = [0, Width - 1];

	private Mock<IGasRegistry> _gases;
	private ICompiledGridAssembly<float> _compiled;
	private PlacementId _placement;
	private IGridLayer<float> _pressure;
	private IGridLayer<float> _temperature;
	private IGridLayer<float> _windX;
	private IGridLayer<float> _windY;
	private IGridLayer<float>[] _gas;
	private IGridLayer<float>[] _condensate;
	private AtmosphereFrames _publisher;
	private IAtmosphereFrames _frames;

	[SetUp]
	public void SetUp() {
		_gases = new Mock<IGasRegistry>( MockBehavior.Strict );
		_gases.Setup( g => g.Count ).Returns( 2 );
		_gases
			.Setup( g => g.GetDefinition( new GasIndex( 1 ) ) )
			.Returns( new GasDefinition( "N2", "Nitrogen", null, null ) );
		DenseGridSource<float> source = new DenseGridSource<float>( AtmosphereSimd.ChunkSize * 3, Height );
		for( int row = 0; row < Height; row++ ) {
			for( int column = 0; column < Width; column++ ) {
				_ = source.TrySetCell( column, row, 0f );
			}
		}
		_compiled = TestGridAssembly.Compile( source );
		_placement = _compiled.Assembly.Placements[0].Id;
		_pressure = _compiled.CreateLayer<float>();
		_temperature = _compiled.CreateLayer<float>();
		_windX = _compiled.CreateLayer<float>();
		_windY = _compiled.CreateLayer<float>();
		_gas = [_compiled.CreateLayer<float>(), _compiled.CreateLayer<float>()];
		_condensate = [_compiled.CreateLayer<float>(), null];
		_publisher = new AtmosphereFrames( _gases.Object );
		_frames = _publisher;
	}

	[TearDown]
	public void TearDown() {
		_compiled.Dispose();
	}

	[Test]
	public void Publish_ThenAcquire_ReadsPublishedValues() {
		_pressure[Width - 1, 2] = 7.0f;
		_temperature[1, 1] = 300.0f;

		Publish( 3, vented: 1.5f );

		using TripleBufferedLease<float> pressure = _frames.AcquirePressure();
		using TripleBufferedLease<float> temperature = _frames.AcquireTemperature();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( pressure.TryGet( Width - 1, 2, out float p ), Is.True );
			Assert.That( p, Is.EqualTo( 7.0f ) );
			Assert.That( temperature.TryGet( 1, 1, out float t ), Is.True );
			Assert.That( t, Is.EqualTo( 300.0f ) );
			Assert.That( _frames.StepCount, Is.EqualTo( 3 ) );
			Assert.That( _frames.Vented, Is.EqualTo( 1.5f ) );
		}
	}

	[Test]
	public void Publish_WhileLeaseHeld_LeaseIsUnchanged() {
		_gas[0][1, 1] = 1.0f;
		Publish( 1 );
		TripleBufferedLease<float> held = _frames.AcquireGas( new GasIndex( 0 ) );

		_gas[0][1, 1] = 2.0f;
		Publish( 2 );
		Publish( 3 );

		Assert.That( held.TryGet( 1, 1, out float before ) && before == 1.0f, Is.True );
		held.Dispose();
		using TripleBufferedLease<float> latest = _frames.AcquireGas( new GasIndex( 0 ) );
		Assert.That( latest.TryGet( 1, 1, out float after ) && after == 2.0f, Is.True );
	}

	[Test]
	public void Publish_CellsAdded_PublishesNewTopology() {
		Publish( 1 );
		const int InNewChunk = ( AtmosphereSimd.ChunkSize * 2 ) + 3;
		Assert.That( _compiled.Assembly.TryAddCell( _placement, Width, 0, 0f ), Is.True );
		Assert.That( _compiled.Assembly.TryAddCell( _placement, InNewChunk, 0, 0f ), Is.True );
		_windX[Width, 0] = 4.0f;
		_windX[InNewChunk, 0] = 5.0f;
		_publisher.InvalidateTopology();

		Publish( 2 );

		using TripleBufferedLease<float> wind = _frames.AcquireWindX();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( wind.TryGet( Width, 0, out float a ) && a == 4.0f, Is.True );
			Assert.That( wind.TryGet( InNewChunk, 0, out float b ) && b == 5.0f, Is.True );
		}
	}

	[Test]
	public void Publish_CellRemoved_IsNoLongerPublished() {
		Publish( 1 );
		Assert.That( _compiled.Assembly.TryRemoveCell( _placement, 1, 1 ), Is.True );
		_publisher.InvalidateTopology();

		Publish( 2 );

		using TripleBufferedLease<float> wind = _frames.AcquireWindY();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( wind.IsPopulated( 1, 1 ), Is.False );
			Assert.That( wind.IsPopulated( 2, 1 ), Is.True );
		}
	}

	[Test]
	public void Publish_TopologyChangedWhileLeaseHeld_HeldLeaseStaysValid() {
		_gas[1][1, 1] = 1.0f;
		Publish( 1 );
		TripleBufferedLease<float> held = _frames.AcquireGas( new GasIndex( 1 ) );

		Assert.That( _compiled.Assembly.TryAddCell( _placement, Width, 0, 0f ), Is.True );
		_gas[1][1, 1] = 2.0f;
		_publisher.InvalidateTopology();
		Publish( 2 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( held.TryGet( 1, 1, out float before ) && before == 1.0f, Is.True );
			Assert.That( held.IsPopulated( Width, 0 ), Is.False );
		}
		held.Dispose();
	}

	[Test]
	public void AcquireCondensate_GasCondenses_ReadsPublishedValues() {
		_condensate[0]![2, 2] = 9.0f;

		Publish( 1 );

		using TripleBufferedLease<float> condensate = _frames.AcquireCondensate( new GasIndex( 0 ) );
		Assert.That( condensate.TryGet( 2, 2, out float value ) && value == 9.0f, Is.True );
	}

	[Test]
	public void AcquireCondensate_GasDoesNotCondense_Throws() {
		Publish( 1 );

		Assert.Throws<ArgumentException>( () => _frames.AcquireCondensate( new GasIndex( 1 ) ) );
	}

	[Test]
	public void Publish_SourceStorageSwapped_ReadsCurrentLayerValues() {
		_pressure[Width - 1, 0] = 11f;
		_temperature[Width - 1, 0] = 22f;
		Publish( 1 );
		_compiled.Swap( _pressure, _temperature );
		Publish( 2 );
		using TripleBufferedLease<float> pressure = _frames.AcquirePressure();
		using TripleBufferedLease<float> temperature = _frames.AcquireTemperature();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( pressure.TryGet( Width - 1, 0, out float p ) && p == 22f, Is.True );
			Assert.That( temperature.TryGet( Width - 1, 0, out float t ) && t == 11f, Is.True );
		}
	}

	[TestCase( false )]
	[TestCase( true )]
	public async Task Publish_ReaderHoldsLeaseAcrossRepeatedPublications_PreservesHeldGeneration(
		bool topologyChanges
	) {
		_pressure[1, 1] = 7f;
		Publish( 1 );
		using ManualResetEventSlim acquired = new ManualResetEventSlim();
		using ManualResetEventSlim published = new ManualResetEventSlim();
		Task reader = Task.Run( () => {
			using TripleBufferedLease<float> held = _frames.AcquirePressure();
			acquired.Set();
			Assert.That( published.Wait( TimeSpan.FromSeconds( 10 ) ), Is.True );
			using( Assert.EnterMultipleScope() ) {
				Assert.That( held.TryGet( 1, 1, out float p ) && p == 7f, Is.True );
				Assert.That( held.IsPopulated( Width, 0 ), Is.False );
			}
		} );
		try {
			Assert.That( acquired.Wait( TimeSpan.FromSeconds( 10 ) ), Is.True );
			if( topologyChanges ) {
				Assert.That( _compiled.Assembly.TryAddCell( _placement, Width, 0, 0f ), Is.True );
				_publisher.InvalidateTopology();
			}
			for( int step = 2; step <= 8; step++ ) {
				_pressure[1, 1] = step;
				Publish( step );
			}
		} finally {
			published.Set();
			await reader.ConfigureAwait( false );
		}
		using TripleBufferedLease<float> latest = _frames.AcquirePressure();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.TryGet( 1, 1, out float p ) && p == 8f, Is.True );
			Assert.That( latest.IsPopulated( Width, 0 ), Is.EqualTo( topologyChanges ) );
		}
	}

	[Test]
	public void Publish_RepeatedSwaps_CopiesEveryQuantityFromCurrentStorage() {
		for( int step = 1; step <= 8; step++ ) {
			foreach( int chunk in _compiled.ChunkLayout.EnumerateChunks() ) {
				_pressure.GetValues( chunk ).Fill( step );
				_temperature.GetValues( chunk ).Fill( step + 10 );
				_windX.GetValues( chunk ).Fill( step + 20 );
				_windY.GetValues( chunk ).Fill( step + 30 );
				_gas[0].GetValues( chunk ).Fill( step + 40 );
				_gas[1].GetValues( chunk ).Fill( step + 50 );
				_condensate[0].GetValues( chunk ).Fill( step + 60 );
			}
			Publish( step );
			using TripleBufferedLease<float> pressure = _frames.AcquirePressure();
			using TripleBufferedLease<float> temperature = _frames.AcquireTemperature();
			using TripleBufferedLease<float> windX = _frames.AcquireWindX();
			using TripleBufferedLease<float> windY = _frames.AcquireWindY();
			using TripleBufferedLease<float> gas0 = _frames.AcquireGas( new GasIndex( 0 ) );
			using TripleBufferedLease<float> gas1 = _frames.AcquireGas( new GasIndex( 1 ) );
			using TripleBufferedLease<float> condensate = _frames.AcquireCondensate( new GasIndex( 0 ) );
			foreach( int column in SampleColumns ) {
				using( Assert.EnterMultipleScope() ) {
					Assert.That( pressure.TryGet( column, 0, out float p ) && p == step, Is.True );
					Assert.That( temperature.TryGet( column, 0, out float t ) && t == step + 10, Is.True );
					Assert.That( windX.TryGet( column, 0, out float x ) && x == step + 20, Is.True );
					Assert.That( windY.TryGet( column, 0, out float y ) && y == step + 30, Is.True );
					Assert.That( gas0.TryGet( column, 0, out float g0 ) && g0 == step + 40, Is.True );
					Assert.That( gas1.TryGet( column, 0, out float g1 ) && g1 == step + 50, Is.True );
					Assert.That( condensate.TryGet( column, 0, out float c ) && c == step + 60, Is.True );
				}
			}
		}
	}

	[Test]
	public void Publish_EmptyChunksThenGrowth_MapsNoncontiguousSourceChunks() {
		Publish( 1 );
		for( int row = 0; row < Height; row++ ) {
			for( int column = AtmosphereSimd.ChunkSize; column < Width; column++ ) {
				Assert.That( _compiled.Assembly.TryRemoveCell( _placement, column, row ), Is.True );
			}
		}
		const int NewColumn = AtmosphereSimd.ChunkSize * 2;
		Assert.That( _compiled.Assembly.TryAddCell( _placement, NewColumn, 0, 0f ), Is.True );
		_pressure[0, 0] = 11f;
		_pressure[NewColumn, 0] = 22f;
		_publisher.InvalidateTopology();
		Publish( 2 );
		using( TripleBufferedLease<float> pressure = _frames.AcquirePressure() ) {
			using( Assert.EnterMultipleScope() ) {
				Assert.That( pressure.ChunkCount, Is.EqualTo( 2 ) );
				Assert.That( pressure.IsPopulated( Width - 1, 0 ), Is.False );
				Assert.That( pressure.TryGet( 0, 0, out float a ) && a == 11f, Is.True );
				Assert.That( pressure.TryGet( NewColumn, 0, out float b ) && b == 22f, Is.True );
			}
		}
		for( int row = 0; row < Height; row++ ) {
			for( int column = 0; column < AtmosphereSimd.ChunkSize; column++ ) {
				Assert.That( _compiled.Assembly.TryRemoveCell( _placement, column, row ), Is.True );
			}
		}
		Assert.That( _compiled.Assembly.TryRemoveCell( _placement, NewColumn, 0 ), Is.True );
		_publisher.InvalidateTopology();
		Publish( 3 );
		using( TripleBufferedLease<float> empty = _frames.AcquirePressure() ) {
			Assert.That( empty.ChunkCount, Is.Zero );
		}
		Assert.That( _compiled.Assembly.TryAddCell( _placement, Width - 1, 0, 0f ), Is.True );
		_pressure[Width - 1, 0] = 33f;
		_publisher.InvalidateTopology();
		Publish( 4 );
		using TripleBufferedLease<float> restored = _frames.AcquirePressure();
		Assert.That( restored.TryGet( Width - 1, 0, out float value ) && value == 33f, Is.True );
	}

	[TestCase( false, false )]
	[TestCase( false, true )]
	[TestCase( true, true )]
	public void Publish_CopyFails_PreservesFramesAndRetriesWithoutAnotherInvalidation(
		bool topologyChanges,
		bool invalidate
	) {
		_pressure[1, 1] = 1f;
		Publish( 1, 2f );
		TripleBufferedLease<float> held = _frames.AcquirePressure();
		if( topologyChanges ) {
			Assert.That( _compiled.Assembly.TryAddCell( _placement, Width, 0, 0f ), Is.True );
			_pressure[Width, 0] = 9f;
		}
		if( invalidate ) {
			_publisher.InvalidateTopology();
		}
		_pressure[1, 1] = 3f;
		IGridLayer<float> windY = _windY;
		_windY = new Mock<IGridLayer<float>>( MockBehavior.Strict ).Object;
		Assert.Throws<MockException>( () => Publish( 2, 4f ) );
		_windY = windY;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( _frames.StepCount, Is.EqualTo( 1 ) );
			Assert.That( _frames.Vented, Is.EqualTo( 2f ) );
			Assert.That( held.TryGet( 1, 1, out float old ) && old == 1f, Is.True );
		}
		held.Dispose();
		using( TripleBufferedLease<float> unchanged = _frames.AcquirePressure() ) {
			Assert.That( unchanged.TryGet( 1, 1, out float old ) && old == 1f, Is.True );
			Assert.That( unchanged.IsPopulated( Width, 0 ), Is.False );
		}
		Publish( 2, 4f );
		using TripleBufferedLease<float> latest = _frames.AcquirePressure();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.TryGet( 1, 1, out float value ) && value == 3f, Is.True );
			Assert.That( latest.IsPopulated( Width, 0 ), Is.EqualTo( topologyChanges ) );
			if( topologyChanges ) {
				Assert.That( latest.TryGet( Width, 0, out float added ) && added == 9f, Is.True );
			}
			Assert.That( _frames.StepCount, Is.EqualTo( 2 ) );
			Assert.That( _frames.Vented, Is.EqualTo( 4f ) );
		}
		Publish( 3 );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void Publish_UnchangedTopology_AllocatesNoManagedMemory(
		bool invalidate
	) {
		Publish( 1 );
		for( int i = 0; i < 100; i++ ) {
			if( invalidate ) {
				_publisher.InvalidateTopology();
			}
			Publish( 2 );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			if( invalidate ) {
				_publisher.InvalidateTopology();
			}
			Publish( 3 );
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That( allocated, Is.Zero );
	}

	private void Publish(
		long step,
		float vented = 0.0f
	) {
		_publisher.Publish( step, vented, _pressure, _temperature, _windX, _windY, _gas, _condensate );
	}

}
