namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereTransportTests {

	private const int X = 2;
	private const int Y = 3;
	private const int Cells = AtmosphereSimd.ChunkSize * AtmosphereSimd.ChunkSize;

	private IAtmosphereTransport _transport;
	private ChunkLayer<float>[] _gas;
	private ChunkLayer<float>[] _gasNext;
	private ChunkLayer<float> _total;
	private ChunkLayer<float> _flowEast;
	private ChunkLayer<float> _flowSouth;
	private ChunkLayer<float> _vent;
	private float[] _outgoing;
	private float[] _newTotal;

	[SetUp]
	public void SetUp() {
		_transport = new AtmosphereTransport();
		_gas = [new ChunkLayer<float>(), new ChunkLayer<float>()];
		_gasNext = [new ChunkLayer<float>(), new ChunkLayer<float>()];
		_total = new ChunkLayer<float>();
		_flowEast = new ChunkLayer<float>();
		_flowSouth = new ChunkLayer<float>();
		_vent = new ChunkLayer<float>();
		_outgoing = new float[Cells];
		_newTotal = new float[Cells];

		// Source cell holds 2 O2 + 8 N2 and pushes 5 east; it also vents 1.
		_gas[0][X, Y] = 2.0f;
		_gas[1][X, Y] = 8.0f;
		_total[X, Y] = 10.0f;
		_flowEast[X, Y] = 5.0f;
		_vent[X, Y] = 1.0f;
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Transport_SourceCell_LosesOutflowProportionally(
		bool full
	) {
		Transport( full ? ChunkMask.Full() : ChunkMask.Cell( X, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _outgoing[Local( X, Y )], Is.EqualTo( 6.0f ).Within( 1e-5f ) );
			Assert.That( _gasNext[0][X, Y], Is.EqualTo( 0.8f ).Within( 1e-5f ) );
			Assert.That( _gasNext[1][X, Y], Is.EqualTo( 3.2f ).Within( 1e-5f ) );
			Assert.That( _newTotal[Local( X, Y )], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Transport_ReceivingCell_GetsUpwindComposition(
		bool full
	) {
		Transport( full ? ChunkMask.Full() : ChunkMask.Cell( X + 1, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _gasNext[0][X + 1, Y], Is.EqualTo( 1.0f ).Within( 1e-5f ) );
			Assert.That( _gasNext[1][X + 1, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
			Assert.That( _newTotal[Local( X + 1, Y )], Is.EqualTo( 5.0f ).Within( 1e-5f ) );
		}
	}

	[Test]
	public void Transport_FullMask_ConservesGasExceptVented() {
		Transport( ChunkMask.Full() );

		float sum = 0.0f;
		foreach( float value in _newTotal ) {
			sum += value;
		}
		Assert.That( sum, Is.EqualTo( 9.0f ).Within( 1e-4f ) );
	}

	private void Transport(
		ulong[] mask
	) {
		_transport.Transport( 0, mask, 1.0f, _gas, _gasNext, _total, _flowEast, _flowSouth, _vent, _outgoing, _newTotal );
	}

	private static int Local(
		int x,
		int y
	) {
		return ( y * AtmosphereSimd.ChunkSize ) + x;
	}

}
