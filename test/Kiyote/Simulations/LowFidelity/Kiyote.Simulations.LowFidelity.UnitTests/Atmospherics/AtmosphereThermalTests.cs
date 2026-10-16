using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereThermalTests {

	private const int X = 2;
	private const int Y = 3;

	private IAtmosphereThermal _thermal;
	private ChunkBuffer<float> _total;
	private ChunkBuffer<float> _temperature;
	private ChunkBuffer<float> _temperatureNext;
	private ChunkBuffer<float> _flowEast;
	private ChunkBuffer<float> _flowSouth;
	private ChunkBuffer<float> _vent;
	private ChunkBuffer<Direction> _connectivity;
	private ChunkBuffer<float> _outgoing;
	private ChunkBuffer<float> _newTotal;

	[SetUp]
	public void SetUp() {
		_thermal = new AtmosphereThermal();
		_total = ChunkBuffer<float>.Chunk();
		_temperature = ChunkBuffer<float>.Padded();
		_temperature.Fill( 300.0f );
		_temperatureNext = ChunkBuffer<float>.Chunk();
		_flowEast = ChunkBuffer<float>.Padded();
		_flowSouth = ChunkBuffer<float>.Padded();
		_vent = ChunkBuffer<float>.Chunk();
		_connectivity = ChunkBuffer<Direction>.Chunk();
		_outgoing = ChunkBuffer<float>.Chunk();
		_newTotal = ChunkBuffer<float>.Chunk();
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_HotGasArrives_MixesByAmount(
		bool full
	) {
		// Cell holds 10 at 300K, receives 10 at 400K from the west.
		_total[X, Y] = 10.0f;
		_temperature[X - 1, Y] = 400.0f;
		_flowEast[X - 1, Y] = 10.0f;
		_newTotal[X, Y] = 20.0f;
		float vented = 0.0f;

		bool changing = Mix( ChunkMask.Create( full, X, Y ), 0.0f, ref vented );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _temperatureNext[X, Y], Is.EqualTo( 350.0f ).Within( 1e-3f ) );
			Assert.That( changing, Is.True );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_HotGasArrivesFromNorthChunk_MixesAcrossBorder(
		bool full
	) {
		_total[X, 0] = 10.0f;
		_temperature[X, -1] = 400.0f;
		_flowSouth[X, -1] = 10.0f;
		_newTotal[X, 0] = 20.0f;
		float vented = 0.0f;

		Mix( ChunkMask.Create( full, X, 0 ), 0.0f, ref vented );

		Assert.That( _temperatureNext[X, 0], Is.EqualTo( 350.0f ).Within( 1e-3f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_OpenFaceWithConduction_ExchangesHeat(
		bool full
	) {
		_total[X, Y] = 10.0f;
		_newTotal[X, Y] = 10.0f;
		_temperature[X + 1, Y] = 400.0f;
		_connectivity[X, Y] = Direction.East;
		float vented = 0.0f;

		Mix( ChunkMask.Create( full, X, Y ), 0.1f, ref vented );

		Assert.That( _temperatureNext[X, Y], Is.EqualTo( 310.0f ).Within( 1e-3f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_Vent_AccumulatesVented(
		bool full
	) {
		_total[X, Y] = 10.0f;
		_newTotal[X, Y] = 7.0f;
		_vent[X, Y] = 3.0f;
		float vented = 1.0f;

		Mix( ChunkMask.Create( full, X, Y ), 0.0f, ref vented );

		Assert.That( vented, Is.EqualTo( 4.0f ).Within( 1e-5f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_Still_IsNotChanging(
		bool full
	) {
		float vented = 0.0f;

		bool changing = Mix( ChunkMask.Create( full, X, Y ), 0.0f, ref vented );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.False );
			Assert.That( _temperatureNext[X, Y], Is.EqualTo( 300.0f ) );
		}
	}

	private bool Mix(
		ulong[] mask,
		float conduction,
		ref float vented
	) {
		return _thermal.Mix(
			mask,
			1.0f,
			conduction,
			_total.Span,
			_temperature.Span,
			_temperatureNext.Span,
			_flowEast.Span,
			_flowSouth.Span,
			_vent.Span,
			_connectivity.Span,
			_outgoing.Span,
			_newTotal.Span,
			ref vented
		);
	}

}
