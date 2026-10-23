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
	private IGridLayer<float> _structure;
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
		_structure = _compiled.CreateLayer<float>();
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
	public void AcquireLatest_BeforePublication_ThrowsAndDoesNotRetainLease() {
		Assert.Throws<InvalidOperationException>( () => _frames.AcquireLatest() );
		Publish( 0 );
		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		Assert.That( frame.StepCount, Is.Zero );
	}

	[Test]
	public void AcquireLatest_WithoutNewPublication_ReturnsTheSameCompleteFrame() {
		PublishValues( 7 );
		using( AtmosphereFrameLease first = _frames.AcquireLatest() ) {
			AssertValues( first );
		}
		using AtmosphereFrameLease second = _frames.AcquireLatest();
		Assert.That( second.StepCount, Is.EqualTo( 7 ) );
		AssertValues( second );
	}

	[Test]
	public void AcquireLatest_StandaloneAtmosphere_RejectsStructuralTemperature() {
		_publisher.Publish( 0, 0f, _pressure, _temperature, _windX, _windY, _gas, _condensate );
		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		Assert.That( frame.HasStructuralTemperature, Is.False );
		Assert.Throws<InvalidOperationException>( () => _ = frame.StructuralTemperature );
	}

	[TestCase( -1 )]
	[TestCase( 2 )]
	public void GetGas_InvalidIndex_Throws(
		int index
	) {
		Publish( 0 );
		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		Assert.Throws<ArgumentOutOfRangeException>( () => frame.GetGas( new GasIndex( index ) ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => frame.GetCondensate( new GasIndex( index ) ) );
	}

	[Test]
	public void Publish_WithoutAdvancingPhysics_UpdatesLayersButPreservesStepCount() {
		PublishValues( 0 );
		using( AtmosphereFrameLease before = _frames.AcquireLatest() ) {
			Assert.That( before.StepCount, Is.Zero );
			Assert.That( before.Pressure[0, 0], Is.Zero );
		}
		_pressure[0, 0] = 42f;
		_structure[0, 0] = 200f;
		Publish( 0, 3f );
		using AtmosphereFrameLease after = _frames.AcquireLatest();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( after.StepCount, Is.Zero );
			Assert.That( after.Pressure[0, 0], Is.EqualTo( 42f ) );
			Assert.That( after.StructuralTemperature[0, 0], Is.EqualTo( 200f ) );
			Assert.That( after.Vented, Is.EqualTo( 3f ) );
		}
	}

	[Test]
	public async Task AcquireLatest_LastLayerCopyInProgress_ReturnsPreviousCompleteFrameWithoutWaiting() {
		PublishValues( 1 );
		_pressure[0, 0] = 42f;
		_structure[0, 0] = 200f;
		using ManualResetEventSlim entered = new ManualResetEventSlim();
		using ManualResetEventSlim proceed = new ManualResetEventSlim();
		_structure = new PausedGridLayer( _structure, entered, proceed );
		Task producer = Task.Run( () => Publish( 2, 9f ) );
		Task reader = Task.CompletedTask;
		try {
			Assert.That( entered.Wait( TimeSpan.FromSeconds( 10 ) ), Is.True );
			reader = Task.Run( () => {
				using AtmosphereFrameLease frame = _frames.AcquireLatest();
				Assert.That( frame.StepCount, Is.EqualTo( 1 ) );
				AssertValues( frame );
			} );
			await reader.WaitAsync( TimeSpan.FromSeconds( 2 ) ).ConfigureAwait( false );
		} finally {
			proceed.Set();
			await Task.WhenAll( producer, reader ).WaitAsync( TimeSpan.FromSeconds( 10 ) ).ConfigureAwait( false );
		}
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.StepCount, Is.EqualTo( 2 ) );
			Assert.That( latest.Vented, Is.EqualTo( 9f ) );
			Assert.That( latest.Pressure[0, 0], Is.EqualTo( 42f ) );
			Assert.That( latest.StructuralTemperature[0, 0], Is.EqualTo( 200f ) );
		}
	}

	[Test]
	public async Task AcquireLatest_ConcurrentPublications_AllLayersAndDiagnosticsBelongToOneUpdate() {
		PublishValues( 0 );
		using ManualResetEventSlim acquired = new ManualResetEventSlim();
		using CancellationTokenSource finished = new CancellationTokenSource();
		int reads = 0;
		Task reader = Task.Run( () => {
			long previousStep = -1;
			do {
				using AtmosphereFrameLease frame = _frames.AcquireLatest();
				acquired.Set();
				Assert.That( frame.StepCount, Is.GreaterThanOrEqualTo( previousStep ) );
				previousStep = frame.StepCount;
				AssertValues( frame );
				Thread.SpinWait( 100 );
				AssertValues( frame );
				reads++;
			} while( !finished.IsCancellationRequested );
		} );
		try {
			Assert.That( acquired.Wait( TimeSpan.FromSeconds( 10 ) ), Is.True );
			for( int step = 1; step <= 2000; step++ ) {
				PublishValues( step );
			}
		} finally {
			await finished.CancelAsync().ConfigureAwait( false );
			await reader.WaitAsync( TimeSpan.FromSeconds( 10 ) ).ConfigureAwait( false );
		}
		Assert.That( reads, Is.GreaterThan( 0 ) );
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		Assert.That( latest.StepCount, Is.EqualTo( 2000 ) );
		AssertValues( latest );
	}

	[Test]
	public void Publish_ThenAcquire_ReadsPublishedValues() {
		_pressure[Width - 1, 2] = 7.0f;
		_temperature[1, 1] = 300.0f;

		Publish( 3, vented: 1.5f );

		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		SimulationFrameLayer pressure = frame.Pressure;
		SimulationFrameLayer temperature = frame.Temperature;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( pressure.TryGet( Width - 1, 2, out float p ), Is.True );
			Assert.That( p, Is.EqualTo( 7.0f ) );
			Assert.That( temperature.TryGet( 1, 1, out float t ), Is.True );
			Assert.That( t, Is.EqualTo( 300.0f ) );
			Assert.That( frame.StepCount, Is.EqualTo( 3 ) );
			Assert.That( frame.Vented, Is.EqualTo( 1.5f ) );
		}
	}

	[Test]
	public void AcquireLatest_LeaseAlreadyHeld_Throws() {
		Publish( 1 );
		using AtmosphereFrameLease held = _frames.AcquireLatest();

		Assert.Throws<InvalidOperationException>( () => _frames.AcquireLatest() );
	}

	[Test]
	public void Publish_StaleLeaseCopyDisposed_PreservesNewerLease() {
		_pressure[1, 1] = 1f;
		Publish( 1 );
		AtmosphereFrameLease first = _frames.AcquireLatest();
		AtmosphereFrameLease stale = first;
		first.Dispose();

		_pressure[1, 1] = 2f;
		Publish( 2 );
		using AtmosphereFrameLease held = _frames.AcquireLatest();
		stale.Dispose();

		for( int step = 3; step <= 8; step++ ) {
			_pressure[1, 1] = step;
			Publish( step );
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( held.Pressure.TryGet( 1, 1, out float value ) && value == 2f, Is.True );
			Assert.That( held.StepCount, Is.EqualTo( 2 ) );
			Assert.Throws<InvalidOperationException>( () => _frames.AcquireLatest() );
		}
	}

	[Test]
	public void AcquireLatest_RepeatedAcquisitions_AllocatesNoManagedMemory() {
		Publish( 1 );
		for( int i = 0; i < 100; i++ ) {
			using AtmosphereFrameLease lease = _frames.AcquireLatest();
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			using AtmosphereFrameLease lease = _frames.AcquireLatest();
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That( allocated, Is.Zero );
	}

	[Test]
	public void Publish_WhileLeaseHeld_LeaseIsUnchanged() {
		_gas[0][1, 1] = 1.0f;
		Publish( 1 );
		AtmosphereFrameLease held = _frames.AcquireLatest();

		_gas[0][1, 1] = 2.0f;
		Publish( 2 );
		Publish( 3 );

		Assert.That( held.GetGas( new GasIndex( 0 ) ).TryGet( 1, 1, out float before ) && before == 1.0f, Is.True );
		Assert.That( held.StepCount, Is.EqualTo( 1 ) );
		held.Dispose();
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		Assert.That( latest.GetGas( new GasIndex( 0 ) ).TryGet( 1, 1, out float after ) && after == 2.0f, Is.True );
		Assert.That( latest.StepCount, Is.EqualTo( 3 ) );
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

		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		SimulationFrameLayer wind = frame.WindX;
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

		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		SimulationFrameLayer wind = frame.WindY;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( wind.IsPopulated( 1, 1 ), Is.False );
			Assert.That( wind.IsPopulated( 2, 1 ), Is.True );
		}
	}

	[Test]
	public void Publish_TopologyChangedWhileLeaseHeld_HeldLeaseStaysValid() {
		_gas[1][1, 1] = 1.0f;
		Publish( 1 );
		using AtmosphereFrameLease held = _frames.AcquireLatest();

		Assert.That( _compiled.Assembly.TryAddCell( _placement, Width, 0, 0f ), Is.True );
		_gas[1][1, 1] = 2.0f;
		_publisher.InvalidateTopology();
		Publish( 2 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( held.GetGas( new GasIndex( 1 ) ).TryGet( 1, 1, out float before ) && before == 1.0f, Is.True );
			Assert.That( held.Pressure.IsPopulated( Width, 0 ), Is.False );
		}
	}

	[Test]
	public void GetCondensate_GasCondenses_ReadsPublishedValues() {
		_condensate[0]![2, 2] = 9.0f;

		Publish( 1 );

		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		SimulationFrameLayer condensate = frame.GetCondensate( new GasIndex( 0 ) );
		Assert.That( condensate.TryGet( 2, 2, out float value ) && value == 9.0f, Is.True );
	}

	[Test]
	public void GetCondensate_GasDoesNotCondense_Throws() {
		Publish( 1 );

		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		Assert.Throws<ArgumentException>( () => frame.GetCondensate( new GasIndex( 1 ) ) );
	}

	[Test]
	public void Publish_SourceStorageSwapped_ReadsCurrentLayerValues() {
		_pressure[Width - 1, 0] = 11f;
		_temperature[Width - 1, 0] = 22f;
		Publish( 1 );
		_compiled.Swap( _pressure, _temperature );
		Publish( 2 );
		using AtmosphereFrameLease frame = _frames.AcquireLatest();
		SimulationFrameLayer pressure = frame.Pressure;
		SimulationFrameLayer temperature = frame.Temperature;
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
			using AtmosphereFrameLease held = _frames.AcquireLatest();
			acquired.Set();
			Assert.That( published.Wait( TimeSpan.FromSeconds( 10 ) ), Is.True );
			using( Assert.EnterMultipleScope() ) {
				Assert.That( held.Pressure.TryGet( 1, 1, out float p ) && p == 7f, Is.True );
				Assert.That( held.Pressure.IsPopulated( Width, 0 ), Is.False );
				Assert.That( held.StepCount, Is.EqualTo( 1 ) );
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
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.Pressure.TryGet( 1, 1, out float p ) && p == 8f, Is.True );
			Assert.That( latest.Pressure.IsPopulated( Width, 0 ), Is.EqualTo( topologyChanges ) );
			Assert.That( latest.StepCount, Is.EqualTo( 8 ) );
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
			using AtmosphereFrameLease frame = _frames.AcquireLatest();
			SimulationFrameLayer pressure = frame.Pressure;
			SimulationFrameLayer temperature = frame.Temperature;
			SimulationFrameLayer windX = frame.WindX;
			SimulationFrameLayer windY = frame.WindY;
			SimulationFrameLayer gas0 = frame.GetGas( new GasIndex( 0 ) );
			SimulationFrameLayer gas1 = frame.GetGas( new GasIndex( 1 ) );
			SimulationFrameLayer condensate = frame.GetCondensate( new GasIndex( 0 ) );
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
		using( AtmosphereFrameLease frame = _frames.AcquireLatest() ) {
			SimulationFrameLayer pressure = frame.Pressure;
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
		using( AtmosphereFrameLease empty = _frames.AcquireLatest() ) {
			Assert.That( empty.Pressure.ChunkCount, Is.Zero );
		}
		Assert.That( _compiled.Assembly.TryAddCell( _placement, Width - 1, 0, 0f ), Is.True );
		_pressure[Width - 1, 0] = 33f;
		_publisher.InvalidateTopology();
		Publish( 4 );
		using AtmosphereFrameLease restored = _frames.AcquireLatest();
		Assert.That( restored.Pressure.TryGet( Width - 1, 0, out float value ) && value == 33f, Is.True );
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
		AtmosphereFrameLease held = _frames.AcquireLatest();
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
			Assert.That( held.Pressure.TryGet( 1, 1, out float old ) && old == 1f, Is.True );
			Assert.That( held.StepCount, Is.EqualTo( 1 ) );
			Assert.That( held.Vented, Is.EqualTo( 2f ) );
		}
		held.Dispose();
		using( AtmosphereFrameLease unchanged = _frames.AcquireLatest() ) {
			Assert.That( unchanged.Pressure.TryGet( 1, 1, out float old ) && old == 1f, Is.True );
			Assert.That( unchanged.Pressure.IsPopulated( Width, 0 ), Is.False );
		}
		Publish( 2, 4f );
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.Pressure.TryGet( 1, 1, out float value ) && value == 3f, Is.True );
			Assert.That( latest.Pressure.IsPopulated( Width, 0 ), Is.EqualTo( topologyChanges ) );
			if( topologyChanges ) {
				Assert.That( latest.Pressure.TryGet( Width, 0, out float added ) && added == 9f, Is.True );
			}
			Assert.That( latest.StepCount, Is.EqualTo( 2 ) );
			Assert.That( latest.Vented, Is.EqualTo( 4f ) );
		}
		Publish( 3 );
	}

	[Test]
	public void Publish_StructuralCopyFails_PreservesTheWholePreviousFrame() {
		PublishValues( 1 );
		IGridLayer<float> structure = _structure;
		_structure = new Mock<IGridLayer<float>>( MockBehavior.Strict ).Object;
		Assert.Throws<MockException>( () => Publish( 2, 99f ) );
		using( AtmosphereFrameLease frame = _frames.AcquireLatest() ) {
			Assert.That( frame.StepCount, Is.EqualTo( 1 ) );
			AssertValues( frame );
		}
		_structure = structure;
		PublishValues( 2 );
		using AtmosphereFrameLease latest = _frames.AcquireLatest();
		Assert.That( latest.StepCount, Is.EqualTo( 2 ) );
		AssertValues( latest );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void PublishAndAcquire_UnchangedTopology_AllocatesNoManagedMemory(
		bool invalidate
	) {
		Publish( 1 );
		for( int i = 0; i < 100; i++ ) {
			if( invalidate ) {
				_publisher.InvalidateTopology();
			}
			Publish( 2 );
			using AtmosphereFrameLease frame = _frames.AcquireLatest();
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			if( invalidate ) {
				_publisher.InvalidateTopology();
			}
			Publish( 3 );
			using AtmosphereFrameLease frame = _frames.AcquireLatest();
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That( allocated, Is.Zero );
	}

	private void Publish(
		long step,
		float vented = 0.0f
	) {
		_publisher.Publish( step, vented, _pressure, _temperature, _windX, _windY, _gas, _condensate, _structure );
	}

	private void PublishValues(
		int step
	) {
		foreach( int chunk in _compiled.ChunkLayout.EnumerateChunks() ) {
			_pressure.GetValues( chunk ).Fill( step );
			_temperature.GetValues( chunk ).Fill( step + 10 );
			_windX.GetValues( chunk ).Fill( step + 20 );
			_windY.GetValues( chunk ).Fill( step + 30 );
			_gas[0].GetValues( chunk ).Fill( step + 40 );
			_gas[1].GetValues( chunk ).Fill( step + 50 );
			_condensate[0].GetValues( chunk ).Fill( step + 60 );
			_structure.GetValues( chunk ).Fill( step + 70 );
		}
		Publish( step, step * 0.5f );
	}

	private static void AssertValues(
		AtmosphereFrameLease frame
	) {
		float step = frame.StepCount;
		foreach( int column in SampleColumns ) {
			using( Assert.EnterMultipleScope() ) {
				Assert.That( frame.Pressure[column, 0], Is.EqualTo( step ) );
				Assert.That( frame.Temperature[column, 0], Is.EqualTo( step + 10 ) );
				Assert.That( frame.WindX[column, 0], Is.EqualTo( step + 20 ) );
				Assert.That( frame.WindY[column, 0], Is.EqualTo( step + 30 ) );
				Assert.That( frame.GetGas( new GasIndex( 0 ) )[column, 0], Is.EqualTo( step + 40 ) );
				Assert.That( frame.GetGas( new GasIndex( 1 ) )[column, 0], Is.EqualTo( step + 50 ) );
				Assert.That( frame.GetCondensate( new GasIndex( 0 ) )[column, 0], Is.EqualTo( step + 60 ) );
				Assert.That( frame.StructuralTemperature[column, 0], Is.EqualTo( step + 70 ) );
				Assert.That( frame.Vented, Is.EqualTo( step * 0.5f ) );
			}
		}
	}

}
