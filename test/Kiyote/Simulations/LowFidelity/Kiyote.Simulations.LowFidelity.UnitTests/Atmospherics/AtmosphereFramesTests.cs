using Kiyote.Buffers.ChunkedGrids;
using Moq;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereFramesTests {

	// Wide enough to span two chunks.
	private const int Width = AtmosphereSimd.ChunkSize + 8;
	private const int Height = 4;

	private Mock<IGasRegistry> _gases;
	private ChunkedGridLayout _layout;
	private TestGridLayer<float> _pressure;
	private TestGridLayer<float> _temperature;
	private TestGridLayer<float> _windX;
	private TestGridLayer<float> _windY;
	private TestGridLayer<float>[] _gas;
	private TestGridLayer<float>[] _condensate;
	private AtmosphereFrames _publisher;
	private IAtmosphereFrames _frames;

	[SetUp]
	public void SetUp() {
		_gases = new Mock<IGasRegistry>( MockBehavior.Strict );
		_gases.Setup( g => g.Count ).Returns( 2 );
		_gases
			.Setup( g => g.GetDefinition( new GasIndex( 1 ) ) )
			.Returns( new GasDefinition( "N2", "Nitrogen", null, null ) );
		_layout = TestGridLayout.Create( Width, Height );
		_pressure = new TestGridLayer<float>( _layout );
		_temperature = new TestGridLayer<float>( _layout );
		_windX = new TestGridLayer<float>( _layout );
		_windY = new TestGridLayer<float>( _layout );
		_gas = [new TestGridLayer<float>( _layout ), new TestGridLayer<float>( _layout )];
		_condensate = [new TestGridLayer<float>( _layout ), null];
		_publisher = new AtmosphereFrames( _gases.Object );
		_frames = _publisher;
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
		_ = _layout.SetPopulated( Width, 0 );
		_ = _layout.SetPopulated( InNewChunk, 0 );
		_windX[Width, 0] = 4.0f;
		_windX[InNewChunk, 0] = 5.0f;

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
		_ = _layout.ClearPopulated( 1, 1 );

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

		_ = _layout.SetPopulated( Width, 0 );
		_gas[1][1, 1] = 2.0f;
		Publish( 2 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( held.TryGet( 1, 1, out float before ) && before == 1.0f, Is.True );
			Assert.That( held.IsPopulated( Width, 0 ), Is.False );
		}
		held.Dispose();
	}

	[Test]
	public void AcquireCondensate_GasCondenses_ReadsPublishedValues() {
		_condensate[0][2, 2] = 9.0f;

		Publish( 1 );

		using TripleBufferedLease<float> condensate = _frames.AcquireCondensate( new GasIndex( 0 ) );
		Assert.That( condensate.TryGet( 2, 2, out float value ) && value == 9.0f, Is.True );
	}

	[Test]
	public void AcquireCondensate_GasDoesNotCondense_Throws() {
		Publish( 1 );

		Assert.Throws<ArgumentException>( () => _frames.AcquireCondensate( new GasIndex( 1 ) ) );
	}

	private void Publish(
		long step,
		float vented = 0.0f
	) {
		_publisher.Publish( step, vented, _pressure, _temperature, _windX, _windY, _gas, _condensate );
	}

}
