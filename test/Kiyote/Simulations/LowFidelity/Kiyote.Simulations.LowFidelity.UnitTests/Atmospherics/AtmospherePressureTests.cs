namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmospherePressureTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmospherePressure _pressure;
	private ChunkBuffer<float> _oxygen;
	private ChunkBuffer<float> _nitrogen;
	private ChunkBuffer<float> _temperature;
	private ChunkBuffer<float> _total;
	private ChunkBuffer<float> _output;

	[SetUp]
	public void SetUp() {
		_pressure = new AtmospherePressure();
		_oxygen = ChunkBuffer<float>.Chunk();
		_oxygen.Fill( 20.0f );
		_nitrogen = ChunkBuffer<float>.Chunk();
		_nitrogen.Fill( 80.0f );
		_temperature = ChunkBuffer<float>.Chunk();
		_temperature.Fill( AtmosphereSimd.ReferenceTemperature * 2.0f );
		_total = ChunkBuffer<float>.Chunk();
		_output = ChunkBuffer<float>.Chunk();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accumulate_TwoGases_WritesSum(
		bool full
	) {
		ulong[] mask = ChunkMask.Create( full, X, Y );

		_pressure.Accumulate( mask, _oxygen.Span, _total.Span );
		_pressure.Accumulate( mask, _nitrogen.Span, _total.Span );

		Assert.That( _total[X, Y], Is.EqualTo( 100.0f ).Within( 1e-3f ) );
	}

	[Test]
	public void Accumulate_CellOutsideMask_IsUntouched() {
		_pressure.Accumulate( ChunkMask.Cell( X, Y ), _oxygen.Span, _total.Span );

		Assert.That( _total[X + 1, Y], Is.Zero );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Compute_TwiceReferenceTemperature_DoublesTotal(
		bool full
	) {
		_total.Fill( 100.0f );

		_pressure.Compute( ChunkMask.Create( full, X, Y ), _total.Span, _temperature.Span, _output.Span );

		Assert.That( _output[X, Y], Is.EqualTo( 200.0f ).Within( 1e-3f ) );
	}

	[Test]
	public void Compute_CellOutsideMask_IsUntouched() {
		_total.Fill( 100.0f );

		_pressure.Compute( ChunkMask.Cell( X, Y ), _total.Span, _temperature.Span, _output.Span );

		Assert.That( _output[X + 1, Y], Is.Zero );
	}

}
