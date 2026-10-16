namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereTransportTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereTransport _transport;
	private ChunkBuffer<float>[] _gas;
	private ChunkBuffer<float>[] _gasNext;
	private ChunkBuffer<float> _total;
	private ChunkBuffer<float> _flowEast;
	private ChunkBuffer<float> _flowSouth;
	private ChunkBuffer<float> _vent;
	private ChunkBuffer<float> _outgoing;
	private ChunkBuffer<float> _newTotal;

	[SetUp]
	public void SetUp() {
		_transport = new AtmosphereTransport();
		_gas = [ChunkBuffer<float>.Padded(), ChunkBuffer<float>.Padded()];
		_gasNext = [ChunkBuffer<float>.Chunk(), ChunkBuffer<float>.Chunk()];
		_total = ChunkBuffer<float>.Padded();
		_flowEast = ChunkBuffer<float>.Padded();
		_flowSouth = ChunkBuffer<float>.Padded();
		_vent = ChunkBuffer<float>.Chunk();
		_outgoing = ChunkBuffer<float>.Chunk();
		_newTotal = ChunkBuffer<float>.Chunk();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Transport_SourceCell_LosesOutflowProportionally(
		bool full
	) {
		SetSource( X, Y );
		_vent[X, Y] = 1.0f;

		Transport( ChunkMask.Create( full, X, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _outgoing[X, Y], Is.EqualTo( 6.0f ).Within( 1e-5f ) );
			Assert.That( _gasNext[0][X, Y], Is.EqualTo( 0.8f ).Within( 1e-5f ) );
			Assert.That( _gasNext[1][X, Y], Is.EqualTo( 3.2f ).Within( 1e-5f ) );
			Assert.That( _newTotal[X, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Transport_ReceivingCell_GetsUpwindComposition(
		bool full
	) {
		SetSource( X, Y );

		Transport( ChunkMask.Create( full, X + 1, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _gasNext[0][X + 1, Y], Is.EqualTo( 1.0f ).Within( 1e-5f ) );
			Assert.That( _gasNext[1][X + 1, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
			Assert.That( _newTotal[X + 1, Y], Is.EqualTo( 5.0f ).Within( 1e-5f ) );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Transport_SourceInWestChunk_ReceivesAcrossBorder(
		bool full
	) {
		SetSource( -1, Y );

		Transport( ChunkMask.Create( full, 0, Y ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _gasNext[0][0, Y], Is.EqualTo( 1.0f ).Within( 1e-5f ) );
			Assert.That( _gasNext[1][0, Y], Is.EqualTo( 4.0f ).Within( 1e-5f ) );
		}
	}

	[Test]
	public void Transport_FullMask_ConservesGasExceptVented() {
		SetSource( X, Y );
		_vent[X, Y] = 1.0f;

		Transport( ChunkMask.Full() );

		float sum = 0.0f;
		foreach( float value in _newTotal.Span ) {
			sum += value;
		}
		Assert.That( sum, Is.EqualTo( 9.0f ).Within( 1e-4f ) );
	}

	// The cell holds 2 O2 + 8 N2 and pushes 5 east.
	private void SetSource(
		int x,
		int y
	) {
		_gas[0][x, y] = 2.0f;
		_gas[1][x, y] = 8.0f;
		_total[x, y] = 10.0f;
		_flowEast[x, y] = 5.0f;
	}

	private void Transport(
		ulong[] mask
	) {
		_transport.Prepare( mask, 1.0f, _flowEast.Span, _flowSouth.Span, _vent.Span, _outgoing.Span, _newTotal.Span );
		for( int g = 0; g < _gas.Length; g++ ) {
			_transport.Transport( mask, 1.0f, _gas[g].Span, _total.Span, _flowEast.Span, _flowSouth.Span, _outgoing.Span, _gasNext[g].Span, _newTotal.Span );
		}
	}

}
