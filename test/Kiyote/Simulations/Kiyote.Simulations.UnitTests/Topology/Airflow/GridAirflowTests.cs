using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;
using Moq;

namespace Kiyote.Simulations.Topology.Airflow.UnitTests;

[TestFixture]
public sealed class GridAirflowTests {

	private const int ChunkSize = 32;

	private Mock<IGridPressure> _pressure;
	private Mock<IGridProjection> _projection;
	private Mock<IGridAdvection> _advection;
	private ISimulationClock _clock;
	private IGridAirflow _airflow;

	[SetUp]
	public void SetUp() {
		_pressure = new Mock<IGridPressure>( MockBehavior.Strict );
		_projection = new Mock<IGridProjection>( MockBehavior.Strict );
		_advection = new Mock<IGridAdvection>( MockBehavior.Strict );
		_clock = new SimulationClock();
		_airflow = new GridAirflow( _pressure.Object, _projection.Object, _advection.Object, _clock );
	}

	[Test]
	public void CreateNeighbourhood_ComposesSubNeighbourhoods_PressureSharesProjectionCombined() {
		ICompiledGridAssembly<float> compiled = Mock.Of<ICompiledGridAssembly<float>>();
		IGridLayer<Direction> connectivity = Mock.Of<IGridLayer<Direction>>();
		IGridLayer<Direction> combined = Mock.Of<IGridLayer<Direction>>();
		ProjectionNeighbourhood projection = new ProjectionNeighbourhood( connectivity, combined );
		AdvectionNeighbourhood advection = new AdvectionNeighbourhood( connectivity, Mock.Of<IGridLayer<Direction>>() );
		_projection.Setup( p => p.CreateNeighbourhood( compiled, connectivity ) ).Returns( projection );
		_advection.Setup( a => a.CreateNeighbourhood( compiled, connectivity ) ).Returns( advection );

		AirflowNeighbourhood result = _airflow.CreateNeighbourhood( compiled, connectivity );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( result.Projection, Is.SameAs( projection ) );
			Assert.That( result.Advection, Is.SameAs( advection ) );
			Assert.That( result.Pressure, Is.SameAs( combined ) );
		}
	}

	[Test]
	public void Update_CallsSimulationsInOrder() {
		ChunkedGridLayout layout = new ChunkedGridLayout( ChunkSize );
		AirflowNeighbourhood neighbourhood = Neighbourhood( Layer<Direction>( layout ) );
		IGridLayer<float> pSrc = Layer<float>( layout );
		IGridLayer<float> pDst = Layer<float>( layout );
		IGridLayer<float> vx = Layer<float>( layout );
		IGridLayer<float> vy = Layer<float>( layout );
		IGridLayer<float> dx = Layer<float>( layout );
		IGridLayer<float> dy = Layer<float>( layout );
		IGridLayer<float> ix = Layer<float>( layout );
		IGridLayer<float> iy = Layer<float>( layout );
		IGridLayer<float> pp = Layer<float>( layout );
		IGridLayer<float> pps = Layer<float>( layout );
		IGridLayer<float> div = Layer<float>( layout );
		IGridLayer<float> cSrc = Layer<float>( layout );
		IGridLayer<float> cDst = Layer<float>( layout );
		MockSequence sequence = new MockSequence();
		_pressure.InSequence( sequence ).Setup( p => p.Update( neighbourhood.Pressure, pSrc, pDst ) );
		_advection.InSequence( sequence ).Setup( a => a.Update( neighbourhood.Advection, vx, vy, vx, ix ) );
		_advection.InSequence( sequence ).Setup( a => a.Update( neighbourhood.Advection, vx, vy, vy, iy ) );
		_projection.InSequence( sequence ).Setup( p => p.Update( neighbourhood.Projection, ix, iy, dx, dy, pp, pps, div ) );
		_advection.InSequence( sequence ).Setup( a => a.Update( neighbourhood.Advection, dx, dy, cSrc, cDst ) );

		_airflow.Update( neighbourhood, pSrc, pDst, vx, vy, dx, dy, ix, iy, pp, pps, div, cSrc, cDst );

		using( Assert.EnterMultipleScope() ) {
			_pressure.Verify( p => p.Update( neighbourhood.Pressure, pSrc, pDst ), Times.Once );
			_advection.Verify( a => a.Update( neighbourhood.Advection, vx, vy, vx, ix ), Times.Once );
			_advection.Verify( a => a.Update( neighbourhood.Advection, vx, vy, vy, iy ), Times.Once );
			_projection.Verify( p => p.Update( neighbourhood.Projection, ix, iy, dx, dy, pp, pps, div ), Times.Once );
			_advection.Verify( a => a.Update( neighbourhood.Advection, dx, dy, cSrc, cDst ), Times.Once );
		}
	}

	[Test]
	public void Update_PressurePeak_AcceleratesIntermediateVelocityAway() {
		ChunkedGridLayout layout = new ChunkedGridLayout( ChunkSize );
		_ = layout.SetPopulatedRun( 4, 5, 3 );
		IGridLayer<Direction> flags = Layer<Direction>( layout );
		_ = flags.Values.TrySet( 4, 5, Direction.East );
		_ = flags.Values.TrySet( 5, 5, Direction.East | Direction.West );
		_ = flags.Values.TrySet( 6, 5, Direction.West );
		AirflowNeighbourhood neighbourhood = Neighbourhood( flags );
		IGridLayer<float> pressure = Layer<float>( layout );
		_ = pressure.Values.TrySet( 5, 5, 1000f );
		IGridLayer<float> ix = Layer<float>( layout );
		IGridLayer<float> iy = Layer<float>( layout );
		_pressure.Setup( p => p.Update( It.IsAny<IGridLayer<Direction>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>() ) );
		_advection.Setup( a => a.Update( It.IsAny<AdvectionNeighbourhood>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>() ) );
		_projection.Setup( p => p.Update( It.IsAny<ProjectionNeighbourhood>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>(), It.IsAny<IGridLayer<float>>() ) );

		_airflow.Update(
			neighbourhood,
			Layer<float>( layout ), pressure,
			Layer<float>( layout ), Layer<float>( layout ),
			Layer<float>( layout ), Layer<float>( layout ),
			ix, iy,
			Layer<float>( layout ), Layer<float>( layout ), Layer<float>( layout ),
			Layer<float>( layout ), Layer<float>( layout )
		);

		float expected = 1000f * _clock.FixedTimeStep * 0.05f * 0.5f;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( ix.Values.GetReference( 6, 5 ), Is.EqualTo( expected ).Within( 1e-6f ) );
			Assert.That( ix.Values.GetReference( 4, 5 ), Is.EqualTo( -expected ).Within( 1e-6f ) );
			Assert.That( ix.Values.GetReference( 5, 5 ), Is.Zero );
			Assert.That( iy.Values.GetReference( 5, 5 ), Is.Zero );
		}
	}

	private static AirflowNeighbourhood Neighbourhood(
		IGridLayer<Direction> pressure
	) {
		return new AirflowNeighbourhood(
			pressure,
			new ProjectionNeighbourhood( Mock.Of<IGridLayer<Direction>>(), pressure ),
			new AdvectionNeighbourhood( Mock.Of<IGridLayer<Direction>>(), Mock.Of<IGridLayer<Direction>>() )
		);
	}

	private static IGridLayer<T> Layer<T>(
		ChunkedGridLayout layout
	) where T : unmanaged {
		Mock<IGridLayer<T>> layer = new Mock<IGridLayer<T>>();
		layer.Setup( l => l.Layout ).Returns( layout );
		layer.Setup( l => l.Values ).Returns( new ChunkedGridLayer<T>( layout ) );
		return layer.Object;
	}

}
