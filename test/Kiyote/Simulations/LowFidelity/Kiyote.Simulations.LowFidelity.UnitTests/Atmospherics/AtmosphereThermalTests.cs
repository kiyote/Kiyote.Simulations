using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereThermalTests {

	private const int X = 2;
	private const int Y = 3;
	private const int Cells = AtmosphereSimd.ChunkSize * AtmosphereSimd.ChunkSize;

	private IAtmosphereThermal _thermal;
	private ChunkLayer<float> _total;
	private ChunkLayer<float> _temperature;
	private ChunkLayer<float> _temperatureNext;
	private ChunkLayer<float> _flowEast;
	private ChunkLayer<float> _flowSouth;
	private ChunkLayer<float> _vent;
	private ChunkLayer<Direction> _connectivity;
	private float[] _outgoing;
	private float[] _newTotal;

	[SetUp]
	public void SetUp() {
		_thermal = new AtmosphereThermal();
		_total = new ChunkLayer<float>();
		_temperature = new ChunkLayer<float>();
		_temperature.Fill( 300.0f );
		_temperatureNext = new ChunkLayer<float>();
		_flowEast = new ChunkLayer<float>();
		_flowSouth = new ChunkLayer<float>();
		_vent = new ChunkLayer<float>();
		_connectivity = new ChunkLayer<Direction>();
		_outgoing = new float[Cells];
		_newTotal = new float[Cells];
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
		_newTotal[Local( X, Y )] = 20.0f;
		float vented = 0.0f;

		bool changing = Mix( full, 0.0f, ref vented );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _temperatureNext[X, Y], Is.EqualTo( 350.0f ).Within( 1e-3f ) );
			Assert.That( changing, Is.True );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_OpenFaceWithConduction_ExchangesHeat(
		bool full
	) {
		_total[X, Y] = 10.0f;
		_newTotal[Local( X, Y )] = 10.0f;
		_temperature[X + 1, Y] = 400.0f;
		_connectivity[X, Y] = Direction.East;
		float vented = 0.0f;

		Mix( full, 0.1f, ref vented );

		Assert.That( _temperatureNext[X, Y], Is.EqualTo( 310.0f ).Within( 1e-3f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_Vent_AccumulatesVented(
		bool full
	) {
		_total[X, Y] = 10.0f;
		_newTotal[Local( X, Y )] = 7.0f;
		_vent[X, Y] = 3.0f;
		float vented = 1.0f;

		Mix( full, 0.0f, ref vented );

		Assert.That( vented, Is.EqualTo( 4.0f ).Within( 1e-5f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Mix_Still_IsNotChanging(
		bool full
	) {
		float vented = 0.0f;

		bool changing = Mix( full, 0.0f, ref vented );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.False );
			Assert.That( _temperatureNext[X, Y], Is.EqualTo( 300.0f ) );
		}
	}

	private bool Mix(
		bool full,
		float conduction,
		ref float vented
	) {
		return _thermal.Mix(
			0,
			full ? ChunkMask.Full() : ChunkMask.Cell( X, Y ),
			1.0f,
			conduction,
			_total,
			_temperature,
			_temperatureNext,
			_flowEast,
			_flowSouth,
			_vent,
			_connectivity,
			_outgoing,
			_newTotal,
			ref vented
		);
	}

	private static int Local(
		int x,
		int y
	) {
		return ( y * AtmosphereSimd.ChunkSize ) + x;
	}

}
