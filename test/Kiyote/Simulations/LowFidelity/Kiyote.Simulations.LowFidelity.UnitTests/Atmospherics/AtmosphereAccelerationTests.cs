using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereAccelerationTests {

	private const int X = 2;
	private const int Y = 3;
	private const float K = 0.5f;

	private IAtmosphereAcceleration _acceleration;
	private ChunkLayer<float> _pressure;
	private ChunkLayer<float> _flowEast;
	private ChunkLayer<float> _flowSouth;
	private ChunkLayer<float> _vent;
	private ChunkLayer<Direction> _connectivity;
	private ChunkLayer<Direction> _vacuum;
	private ChunkLayer<bool> _permeable;

	[SetUp]
	public void SetUp() {
		_acceleration = new AtmosphereAcceleration();
		_pressure = new ChunkLayer<float>();
		_pressure.Fill( 100.0f );
		_flowEast = new ChunkLayer<float>();
		_flowSouth = new ChunkLayer<float>();
		_vent = new ChunkLayer<float>();
		_connectivity = new ChunkLayer<Direction>();
		_connectivity.Fill( Direction.North | Direction.East | Direction.South | Direction.West );
		_vacuum = new ChunkLayer<Direction>();
		_permeable = new ChunkLayer<bool>();
		_permeable.Fill( true );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_LowerPressureEast_FlowsEast(
		bool full
	) {
		_pressure[X + 1, Y] = 60.0f;

		Accelerate( full, damp: 1.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _flowEast[X, Y], Is.EqualTo( K * 40.0f ).Within( 1e-4f ) );
			Assert.That( _flowSouth[X, Y], Is.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_ExistingFlow_IsDamped(
		bool full
	) {
		_flowSouth[X, Y] = 10.0f;

		Accelerate( full, damp: 0.5f );

		Assert.That( _flowSouth[X, Y], Is.EqualTo( 5.0f ).Within( 1e-4f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_ClosedFace_ZeroesFlow(
		bool full
	) {
		_pressure[X + 1, Y] = 60.0f;
		_flowEast[X, Y] = 10.0f;
		_connectivity[X, Y] = Direction.South;

		Accelerate( full, damp: 1.0f );

		Assert.That( _flowEast[X, Y], Is.Zero );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_VacuumFace_Vents(
		bool full
	) {
		_vacuum[X, Y] = Direction.North;

		Accelerate( full, damp: 1.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _vent[X, Y], Is.EqualTo( K * 100.0f ).Within( 1e-4f ) );
			Assert.That( _vent[X + 1, Y], Is.Zero );
		}
	}

	private void Accelerate(
		bool full,
		float damp
	) {
		_acceleration.Accelerate(
			0,
			full ? ChunkMask.Full() : ChunkMask.Cell( X, Y ),
			K,
			damp,
			eastOpen: false,
			southOpen: false,
			_pressure,
			_flowEast,
			_flowSouth,
			_vent,
			_connectivity,
			_vacuum,
			_permeable
		);
	}

}
