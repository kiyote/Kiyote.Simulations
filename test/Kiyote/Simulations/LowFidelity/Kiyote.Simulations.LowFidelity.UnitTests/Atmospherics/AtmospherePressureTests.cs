using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmospherePressureTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmospherePressure _pressure;
	private ChunkLayer<float>[] _gas;
	private ChunkLayer<float> _temperature;
	private ChunkLayer<float> _total;
	private ChunkLayer<float> _output;

	[SetUp]
	public void SetUp() {
		_pressure = new AtmospherePressure();
		_gas = [new ChunkLayer<float>(), new ChunkLayer<float>()];
		_gas[0].Fill( 20.0f );
		_gas[1].Fill( 80.0f );
		_temperature = new ChunkLayer<float>();
		_temperature.Fill( AtmosphereSimd.ReferenceTemperature * 2.0f );
		_total = new ChunkLayer<float>();
		_output = new ChunkLayer<float>();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Compute_TwiceReferenceTemperature_DoublesTotal(
		bool full
	) {
		_pressure.Compute( 0, Mask( full ), _gas, _temperature, _total, _output );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _total[X, Y], Is.EqualTo( 100.0f ).Within( 1e-3f ) );
			Assert.That( _output[X, Y], Is.EqualTo( 200.0f ).Within( 1e-3f ) );
		}
	}

	[Test]
	public void Compute_CellOutsideMask_IsUntouched() {
		_pressure.Compute( 0, ChunkMask.Cell( X, Y ), _gas, _temperature, _total, _output );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _total[X + 1, Y], Is.Zero );
			Assert.That( _output[X + 1, Y], Is.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void SumGas_TwoGases_WritesSum(
		bool full
	) {
		_pressure.SumGas( 0, Mask( full ), _gas, _total );

		Assert.That( _total[X, Y], Is.EqualTo( 100.0f ).Within( 1e-3f ) );
	}

	private static ulong[] Mask(
		bool full
	) {
		return full ? ChunkMask.Full() : ChunkMask.Cell( X, Y );
	}

}
