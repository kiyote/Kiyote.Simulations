namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereWindTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereWind _wind;
	private ChunkBuffer<float> _temperature;
	private ChunkBuffer<float> _flowEast;
	private ChunkBuffer<float> _flowSouth;
	private ChunkBuffer<float> _pressure;
	private ChunkBuffer<float> _windX;
	private ChunkBuffer<float> _windY;

	[SetUp]
	public void SetUp() {
		_wind = new AtmosphereWind();
		_temperature = ChunkBuffer<float>.Chunk();
		_temperature.Fill( AtmosphereSimd.ReferenceTemperature * 2.0f );
		_flowEast = ChunkBuffer<float>.Padded();
		_flowSouth = ChunkBuffer<float>.Padded();
		_pressure = ChunkBuffer<float>.Chunk();
		_windX = ChunkBuffer<float>.Chunk();
		_windY = ChunkBuffer<float>.Chunk();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_TotalGas_BecomesPressure(
		bool full
	) {
		_pressure[X, Y] = 100.0f;

		Publish( ChunkMask.Create( full, X, Y ), 1.0f );

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

		Publish( ChunkMask.Create( full, X, Y ), 2.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _windX[X, Y], Is.EqualTo( 2.0f ).Within( 1e-4f ) );
			Assert.That( _windY[X, Y], Is.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_SouthwardFlowFromNorthChunk_ProducesSouthWind(
		bool full
	) {
		_pressure[X, 0] = 100.0f;
		_flowSouth[X, -1] = 10.0f;
		_flowSouth[X, 0] = 10.0f;

		Publish( ChunkMask.Create( full, X, 0 ), 2.0f );

		Assert.That( _windY[X, 0], Is.EqualTo( 2.0f ).Within( 1e-4f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Publish_EmptyCell_HasNoWind(
		bool full
	) {
		_flowSouth[X, Y] = 10.0f;
		_windY[X, Y] = 7.0f;

		Publish( ChunkMask.Create( full, X, Y ), 1.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _windX[X, Y], Is.Zero );
			Assert.That( _windY[X, Y], Is.Zero );
		}
	}

	private void Publish(
		ulong[] mask,
		float windScale
	) {
		_wind.Publish(
			mask,
			windScale,
			_temperature.Span,
			_flowEast.Span,
			_flowSouth.Span,
			_pressure.Span,
			_windX.Span,
			_windY.Span
		);
	}

}
