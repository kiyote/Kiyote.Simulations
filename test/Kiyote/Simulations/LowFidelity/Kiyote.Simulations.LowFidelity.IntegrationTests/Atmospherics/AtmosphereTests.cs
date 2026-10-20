using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;

[TestFixture]
internal sealed class AtmosphereTests {

	private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds( 100 );
	private static readonly TestCell Wall = new TestCell( IsWalkable: false, IsGasPermeable: false, IsVacuum: false );
	private static readonly TestCell Floor = new TestCell( IsWalkable: true, IsGasPermeable: true, IsVacuum: false );

	private IGridAtmospherics _atmospherics;
	private IGridAssembly<TestCell> _assembly;
	private ICompiledGridAssembly<TestCell> _compiled;
	private PlacementId _placement;
	private IAtmosphere _atmosphere;

	[SetUp]
	public void SetUp() {
		IGasRegistryBuilder builder = new GasRegistryBuilder( [
			new TestGasDefinitionSource()
		] );
		IGasRegistry gasRegistry = builder.Build();
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( gasRegistry )
			.AddSingleton<IAtmosphericsSettings, TestAtmosphericsSettings>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		_atmospherics = services.GetRequiredService<IGridAtmospherics>();
		_atmosphere = Create( AsciiGridSource.Create( AsciiGridSource.Map1 ) );
	}

	[TearDown]
	public void TearDown() {
		_atmosphere?.Dispose();
		_compiled?.Dispose();
	}

	[Test]
	public void Advance_GasPumps_AtmosphereIsAdded() {
		GasIndex oxygen = _atmosphere.Gases.GetIndex( "O2" );
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		(int Column, int Row, GasIndex Gas)[] pumps = [
			( 18, 7, oxygen ),
			( 17, 7, nitrogen )
		];
		const float pumpRate = 10.0f; // amount per second
		int frames = (int)( TimeSpan.FromSeconds( 10 ) / Frame );

		for( int i = 0; i < frames; i++ ) {
			foreach( (int column, int row, GasIndex gas) in pumps ) {
				_atmosphere.AddGas( column, row, gas, pumpRate * (float)Frame.TotalSeconds );
			}
			_ = _atmosphere.Update( Frame );
		}

		// Sample the interior of the room the pumps sit in (inside the X walls).
		float oxygenTotal = 0.0f;
		float nitrogenTotal = 0.0f;
		int cellsWithGas = 0;
		int cells = 0;
		for( int row = 1; row <= 12; row++ ) {
			for( int column = 1; column <= 18; column++ ) {
				cells++;
				oxygenTotal += _atmosphere.GetGas( oxygen )[column, row];
				nitrogenTotal += _atmosphere.GetGas( nitrogen )[column, row];
				if( _atmosphere.GetTotalGas( column, row ) > 0.0f ) {
					cellsWithGas++;
				}
			}
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( oxygenTotal, Is.GreaterThan( 0.0f ) );
			Assert.That( nitrogenTotal, Is.GreaterThan( 0.0f ) );
			Assert.That( cellsWithGas, Is.EqualTo( cells ), "Gas should have spread throughout the room." );
		}
	}

	[Test]
	public void Advance_SealedRoomAcrossChunks_ConservesGasAndSpreads() {
		// Wide enough to span three chunks, so gas has to cross two chunk borders.
		const int Width = ( AtmosphereSimd.ChunkSize * 2 ) + 6;
		const int Height = 6;
		const float Added = 1000.0f;
		_atmosphere.Dispose();
		_atmosphere = Create( Room( Width, Height ) );
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		_atmosphere.AddGas( 2, 2, nitrogen, Added );

		float vented = 0.0f;
		for( int i = 0; i < 400; i++ ) {
			_ = _atmosphere.Update( Frame );
			vented += _atmosphere.Vented;
		}

		float total = 0.0f;
		for( int row = 0; row < Height; row++ ) {
			for( int column = 0; column < Width; column++ ) {
				total += _atmosphere.GetTotalGas( column, row );
			}
		}
		using( Assert.EnterMultipleScope() ) {
			Assert.That( vented, Is.Zero );
			Assert.That( total, Is.EqualTo( Added ).Within( Added * 0.001f ) );
			Assert.That( _atmosphere.GetTotalGas( Width - 3, Height - 3 ), Is.GreaterThan( 0.0f ), "Gas should cross the chunk borders." );
		}
	}

	[Test]
	public void Frames_LeaseHeldWhileAdvancing_LeaseIsUnchanged() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		IAtmosphereFrames frames = _atmosphere.Frames;
		_atmosphere.AddGas( 5, 5, nitrogen, 100.0f );
		_ = _atmosphere.Update( Frame );

		long step = frames.StepCount;
		TripleBufferedLease<float> held = frames.AcquireGas( nitrogen );
		float[] gas = Snapshot( held );
		for( int i = 0; i < 10; i++ ) {
			_atmosphere.AddGas( 5, 5, nitrogen, 100.0f );
			_ = _atmosphere.Update( Frame );
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( Snapshot( held ), Is.EqualTo( gas ) );
			Assert.That( held.TryGet( 5, 5, out float value ) && value > 0.0f, Is.True );
		}
		held.Dispose();

		using TripleBufferedLease<float> latest = frames.AcquireGas( nitrogen );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( frames.StepCount, Is.EqualTo( step + 10 ) );
			Assert.That( latest.TryGet( 5, 5, out float value ), Is.True );
			Assert.That( value, Is.EqualTo( _atmosphere.GetGas( nitrogen )[5, 5] ) );
		}
	}

	[Test]
	public void Frames_Pressure_MatchesLiveLayer() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		_atmosphere.AddGas( 5, 5, nitrogen, 100.0f );

		_ = _atmosphere.Update( Frame );

		using TripleBufferedLease<float> pressure = _atmosphere.Frames.AcquirePressure();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( pressure.TryGet( 5, 5, out float value ), Is.True );
			Assert.That( value, Is.EqualTo( _atmosphere.Pressure[5, 5] ) );
			Assert.That( pressure.IsPopulated( 25, 5 ), Is.False, "Unoccupied cells are not published." );
		}
	}

	[Test]
	public void Frames_CellAddedAndInvalidated_IsPublished() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		Assert.That( _assembly.TryAddCell( _placement, 25, 5, Floor ), Is.True );
		_atmosphere.InvalidateTopology( new Rect( 25, 5, 1, 1 ) );
		_atmosphere.AddGas( 25, 5, nitrogen, 10.0f );

		_ = _atmosphere.Update( Frame );

		using TripleBufferedLease<float> gas = _atmosphere.Frames.AcquireGas( nitrogen );
		Assert.That( gas.TryGet( 25, 5, out float value ) && value > 0.0f, Is.True );
	}

	[Test]
	public void Frames_ReaderThread_SeesUnchangingLeasesAndIncreasingSteps() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		IAtmosphereFrames frames = _atmosphere.Frames;
		using CancellationTokenSource done = new CancellationTokenSource();
		long lastStep = -1;
		int reads = 0;
		string failure = null;

		Thread reader = new Thread( () => {
			while( !done.IsCancellationRequested && failure is null ) {
				long step = frames.StepCount;
				using TripleBufferedLease<float> held = frames.AcquireGas( nitrogen );
				float before = SumOf( held );
				Thread.SpinWait( 100 );
				float after = SumOf( held );
				if( step < lastStep ) {
					failure = $"Step went backwards: {step} < {lastStep}";
				} else if( before != after ) {
					failure = $"Lease changed while held at step {step}.";
				}
				lastStep = step;
				reads++;
			}
		} );
		reader.Start();
		for( int i = 0; i < 500; i++ ) {
			_atmosphere.AddGas( 5, 5, nitrogen, 10.0f );
			_ = _atmosphere.Update( Frame );
		}
		done.Cancel();
		reader.Join();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( failure, Is.Null );
			Assert.That( reads, Is.GreaterThan( 0 ) );
		}
	}

	private IAtmosphere Create(
		DenseGridSource<TestCell> ship
	) {
		_compiled?.Dispose();
		_assembly = new GridAssembly<TestCell>();
		AttachResult attached = _assembly.TryAttach( ship, 0, 0 );
		Assert.That( attached.Succeeded, Is.True );
		_placement = attached.Placement;
		_compiled = new GridCompiler().Compile( _assembly, AtmosphereSimd.ChunkSize );
		return _atmospherics.Create( _compiled, new TestCellStrategy() );
	}

	// Walls all round a permeable floor.
	private static DenseGridSource<TestCell> Room(
		int width,
		int height
	) {
		DenseGridSource<TestCell> room = new DenseGridSource<TestCell>( width, height );
		for( int row = 0; row < height; row++ ) {
			for( int column = 0; column < width; column++ ) {
				bool edge = row == 0 || column == 0 || row == height - 1 || column == width - 1;
				_ = room.TrySetCell( column, row, edge ? Wall : Floor );
			}
		}
		return room;
	}

	private static float[] Snapshot(
		TripleBufferedLease<float> lease
	) {
		List<float> values = [];
		foreach( GridChunk<float> chunk in lease.EnumerateChunks() ) {
			values.AddRange( chunk.Values );
		}
		return [.. values];
	}

	private static float SumOf(
		TripleBufferedLease<float> lease
	) {
		float sum = 0.0f;
		foreach( GridChunk<float> chunk in lease.EnumerateChunks() ) {
			foreach( float value in chunk.Values ) {
				sum += value;
			}
		}
		return sum;
	}
}
