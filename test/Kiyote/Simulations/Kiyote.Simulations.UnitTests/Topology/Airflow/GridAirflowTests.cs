using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;
using Moq;

namespace Kiyote.Simulations.Topology.Airflow.UnitTests;

[TestFixture]
public sealed class GridAirflowTests {

	private Mock<IGridPressure> _pressure;
	private Mock<IGridProjection> _projection;
	private Mock<IGridAdvection> _advection;
	private IGridAirflow _airflow;

	[SetUp]
	public void SetUp() {
		_pressure = new Mock<IGridPressure>( MockBehavior.Strict );
		_projection = new Mock<IGridProjection>( MockBehavior.Strict );
		_advection = new Mock<IGridAdvection>( MockBehavior.Strict );
		_airflow = new GridAirflow( _pressure.Object, _projection.Object, _advection.Object, new SimulationClock() );
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
	public void Update_PressureDestinationWithoutHalo_ThrowsAfterAdvectingVelocity() {
		AirflowNeighbourhood neighbourhood = new AirflowNeighbourhood(
			Mock.Of<IGridLayer<Direction>>(),
			new ProjectionNeighbourhood( Mock.Of<IGridLayer<Direction>>(), Mock.Of<IGridLayer<Direction>>() ),
			new AdvectionNeighbourhood( Mock.Of<IGridLayer<Direction>>(), Mock.Of<IGridLayer<Direction>>() )
		);
		IGridLayer<float> pSrc = Layer();
		IGridLayer<float> pDst = Layer( halo: 0 );
		IGridLayer<float> vx = Layer();
		IGridLayer<float> vy = Layer();
		IGridLayer<float> ix = Layer();
		IGridLayer<float> iy = Layer();
		MockSequence sequence = new MockSequence();
		_pressure.InSequence( sequence ).Setup( p => p.Update( neighbourhood.Pressure, pSrc, pDst ) );
		_advection.InSequence( sequence ).Setup( a => a.Update( neighbourhood.Advection, vx, vy, vx, ix ) );
		_advection.InSequence( sequence ).Setup( a => a.Update( neighbourhood.Advection, vx, vy, vy, iy ) );

		Assert.Throws<ArgumentOutOfRangeException>( () => _airflow.Update(
			neighbourhood,
			pSrc, pDst,
			vx, vy,
			Layer(), Layer(),
			ix, iy,
			Layer(), Layer(), Layer(),
			Layer(), Layer()
		) );

		using( Assert.EnterMultipleScope() ) {
			_pressure.Verify( p => p.Update( neighbourhood.Pressure, pSrc, pDst ), Times.Once );
			_advection.Verify( a => a.Update( neighbourhood.Advection, vx, vy, vx, ix ), Times.Once );
			_advection.Verify( a => a.Update( neighbourhood.Advection, vx, vy, vy, iy ), Times.Once );
			_projection.VerifyNoOtherCalls();
		}
	}

	private static IGridLayer<float> Layer(
		int halo = 1
	) {
		Mock<IGridLayer<float>> layer = new Mock<IGridLayer<float>>();
		layer.Setup( l => l.Halo ).Returns( halo );
		return layer.Object;
	}

}
