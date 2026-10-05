using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;

[TestFixture]
internal sealed class AtmosphereTests {

	private IAtmosphere _atmosphere;
	private TestCellStrategy _cellStrategy;

	[SetUp]
	public void SetUp() {
		IGasRegistryBuilder builder = new GasRegistryBuilder( [
			new TestGasDefinitionSource()
		] );
		IGasRegistry gasRegistry = builder.Build();
		_cellStrategy = new TestCellStrategy();
		DenseGridSource<TestCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		IGridAssembly<TestCell> assembly = new GridAssembly<TestCell>();
		Assert.That( assembly.TryAttach( ship, 0, 0 ).Succeeded, Is.True );
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( gasRegistry )
			.AddSingleton<IAtmosphericsSettings, TestAtmosphericsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		IGridAtmospherics atmospherics = services.GetRequiredService<IGridAtmospherics>();
		_atmosphere = atmospherics.Create( assembly, _cellStrategy );
	}

	[TearDown]
	public void TearDown() {
		_atmosphere?.Dispose();
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
		TimeSpan frame = TimeSpan.FromMilliseconds( 100 );
		int frames = (int)( TimeSpan.FromSeconds( 10 ) / frame );

		for( int i = 0; i < frames; i++ ) {
			foreach( (int column, int row, GasIndex gas) in pumps ) {
				_atmosphere.AddGas( column, row, gas, pumpRate * (float)frame.TotalSeconds );
			}
			_ = _atmosphere.Advance( frame );
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
	public void AcquireFrame_HeldWhileAdvancing_FrameIsUnchanged() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		TimeSpan frame = TimeSpan.FromMilliseconds( 100 );
		_atmosphere.AddGas( 5, 5, nitrogen, 100.0f );
		_ = _atmosphere.Advance( frame );

		IAtmosphereFrame held = _atmosphere.AcquireFrame();
		long step = held.StepCount;
		float[] gas = held.GetGas( nitrogen ).ToArray();
		float[] pressure = held.Pressure.ToArray();
		for( int i = 0; i < 10; i++ ) {
			_atmosphere.AddGas( 5, 5, nitrogen, 100.0f );
			_ = _atmosphere.Advance( frame );
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( held.StepCount, Is.EqualTo( step ) );
			Assert.That( held.GetGas( nitrogen ).ToArray(), Is.EqualTo( gas ) );
			Assert.That( held.Pressure.ToArray(), Is.EqualTo( pressure ) );
			Assert.That( held.GetGas( nitrogen )[held.IndexOf( 5, 5 )], Is.GreaterThan( 0.0f ) );
		}
		_atmosphere.ReleaseFrame( held );

		IAtmosphereFrame latest = _atmosphere.AcquireFrame();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( latest.StepCount, Is.EqualTo( step + 10 ) );
			Assert.That( latest.GetGas( nitrogen )[latest.IndexOf( 5, 5 )], Is.EqualTo( _atmosphere.GetGas( nitrogen )[5, 5] ) );
		}
		_atmosphere.ReleaseFrame( latest );
	}

	[Test]
	public void AcquireFrame_AlreadyHeld_Throws() {
		IAtmosphereFrame held = _atmosphere.AcquireFrame();

		Assert.Throws<InvalidOperationException>( () => _atmosphere.AcquireFrame() );

		_atmosphere.ReleaseFrame( held );
	}

	[Test]
	public void AcquireFrame_ReaderThread_SeesConsistentIncreasingFrames() {
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		TimeSpan frame = TimeSpan.FromMilliseconds( 100 );
		using CancellationTokenSource done = new CancellationTokenSource();
		long lastStep = -1;
		int frames = 0;
		string failure = null;

		Thread reader = new Thread( () => {
			while( !done.IsCancellationRequested && failure is null ) {
				IAtmosphereFrame held = _atmosphere.AcquireFrame();
				long step = held.StepCount;
				float before = SumOf( held.GetGas( nitrogen ) );
				Thread.SpinWait( 100 );
				float after = SumOf( held.GetGas( nitrogen ) );
				if( step < lastStep ) {
					failure = $"Step went backwards: {step} < {lastStep}";
				} else if( before != after || held.StepCount != step ) {
					failure = $"Frame {step} changed while held.";
				}
				lastStep = step;
				frames++;
				_atmosphere.ReleaseFrame( held );
			}
		} );
		reader.Start();
		for( int i = 0; i < 500; i++ ) {
			_atmosphere.AddGas( 5, 5, nitrogen, 10.0f );
			_ = _atmosphere.Advance( frame );
		}
		done.Cancel();
		reader.Join();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( failure, Is.Null );
			Assert.That( frames, Is.GreaterThan( 0 ) );
		}
	}

	private static float SumOf(
		ReadOnlySpan<float> values
	) {
		float sum = 0.0f;
		foreach( float value in values ) {
			sum += value;
		}
		return sum;
	}
}
