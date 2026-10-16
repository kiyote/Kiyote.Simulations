using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereAccelerationTests {

	private const int X = 2;
	private const int Y = 3;
	private const int Last = AtmosphereSimd.ChunkSize - 1;
	private const float K = 0.5f;

	private IAtmosphereAcceleration _acceleration;
	private ChunkBuffer<float> _pressure;
	private ChunkBuffer<float> _flowEast;
	private ChunkBuffer<float> _flowSouth;
	private ChunkBuffer<float> _vent;
	private ChunkBuffer<Direction> _connectivity;
	private ChunkBuffer<Direction> _vacuum;
	private ChunkBuffer<bool> _permeable;

	[SetUp]
	public void SetUp() {
		_acceleration = new AtmosphereAcceleration();
		_pressure = ChunkBuffer<float>.Padded();
		_pressure.Fill( 100.0f );
		_flowEast = ChunkBuffer<float>.Chunk();
		_flowSouth = ChunkBuffer<float>.Chunk();
		_vent = ChunkBuffer<float>.Chunk();
		_connectivity = ChunkBuffer<Direction>.Chunk();
		_connectivity.Fill( Direction.North | Direction.East | Direction.South | Direction.West );
		_vacuum = ChunkBuffer<Direction>.Chunk();
		_permeable = ChunkBuffer<bool>.Chunk();
		_permeable.Fill( true );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_LowerPressureEast_FlowsEast(
		bool full
	) {
		_pressure[X + 1, Y] = 60.0f;

		Accelerate( ChunkMask.Create( full, X, Y ), damp: 1.0f );

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

		Accelerate( ChunkMask.Create( full, X, Y ), damp: 0.5f );

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

		Accelerate( ChunkMask.Create( full, X, Y ), damp: 1.0f );

		Assert.That( _flowEast[X, Y], Is.Zero );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_VacuumFace_Vents(
		bool full
	) {
		_vacuum[X, Y] = Direction.North;

		Accelerate( ChunkMask.Create( full, X, Y ), damp: 1.0f );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _vent[X, Y], Is.EqualTo( K * 100.0f ).Within( 1e-4f ) );
			Assert.That( _vent[X + 1, Y], Is.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_EastChunkOpen_ReadsPressureAcrossBorder(
		bool full
	) {
		_pressure[Last + 1, Y] = 60.0f;

		Accelerate( ChunkMask.Create( full, Last, Y ), damp: 1.0f, eastOpen: true );

		Assert.That( _flowEast[Last, Y], Is.EqualTo( K * 40.0f ).Within( 1e-4f ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_EastChunkClosed_DoesNotFlowAcrossBorder(
		bool full
	) {
		_pressure[Last + 1, Y] = 60.0f;

		Accelerate( ChunkMask.Create( full, Last, Y ), damp: 1.0f, eastOpen: false );

		Assert.That( _flowEast[Last, Y], Is.Zero );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Accelerate_SouthChunkOpen_ReadsPressureAcrossBorder(
		bool full
	) {
		_pressure[X, Last + 1] = 60.0f;

		Accelerate( ChunkMask.Create( full, X, Last ), damp: 1.0f, southOpen: true );

		Assert.That( _flowSouth[X, Last], Is.EqualTo( K * 40.0f ).Within( 1e-4f ) );
	}

	private void Accelerate(
		ulong[] mask,
		float damp,
		bool eastOpen = false,
		bool southOpen = false
	) {
		_acceleration.Accelerate(
			mask,
			K,
			damp,
			eastOpen,
			southOpen,
			_pressure.Span,
			_flowEast.Span,
			_flowSouth.Span,
			_vent.Span,
			_connectivity.Span,
			_vacuum.Span,
			_permeable.Span
		);
	}

}
