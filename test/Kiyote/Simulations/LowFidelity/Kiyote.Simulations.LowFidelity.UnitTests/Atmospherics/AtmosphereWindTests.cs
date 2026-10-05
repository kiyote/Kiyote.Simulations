namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereWindTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereWind _wind;
	private ChunkLayer<float> _temperature;
	private ChunkLayer<float> _flowEast;
	private ChunkLayer<float> _flowSouth;
	private ChunkLayer<float> _pressure;
	private ChunkLayer<float> _windX;
	private ChunkLayer<float> _windY;

	[SetUp]
	public void SetUp() {
		_wind = new AtmosphereWind();
		_temperature = new ChunkLayer<float>();
		_temperature.Fill( AtmosphereSimd.ReferenceTemperature * 2.0f );
		_flowEast = new ChunkLayer<float>();
		_flowSouth = new ChunkLayer<float>();
		_pressure = new ChunkLayer<float>();
		_windX = new ChunkLayer<float>();
		_windY = new ChunkLayer<float>();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_TotalGas_BecomesPressure(
		bool full
	) {
		_pressure[X, Y] = 100.0f;

		Publish( full, 1.0f );

		Assert.That( _pressure[X, Y], Is.EqualTo( 200.0f ).Within( 1e-3f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_EastwardFlow_ProducesEastWind(
		bool full
	) {
		// v = ( 10 + 10 ) / 2 / 100 = 0.1; wind = scale * total * |v| * v = 2 * 100 * 0.1 * 0.1 = 2.
		_pressure[X, Y] = 100.0f;
		_flowEast[X - 1, Y] = 10.0f;
		_flowEast[X, Y] = 10.0f;

		Publish( full, 2.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _windX[X, Y], Is.EqualTo( 2.0f ).Within( 1e-4f ) );
			Assert.That( _windY[X, Y], Is.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_EmptyCell_HasNoWind(
		bool full
	) {
		_flowSouth[X, Y] = 10.0f;
		_windY[X, Y] = 7.0f;

		Publish( full, 1.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _windX[X, Y], Is.Zero );
			Assert.That( _windY[X, Y], Is.Zero );
		}
	}

	private void Publish(
		bool full,
		float windScale
	) {
		_wind.Publish(
			0,
			full ? ChunkMask.Full() : ChunkMask.Cell( X, Y ),
			windScale,
			_temperature,
			_flowEast,
			_flowSouth,
			_pressure,
			_windX,
			_windY
		);
	}

}
