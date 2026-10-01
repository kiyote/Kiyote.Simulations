using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Moq;

namespace Kiyote.Simulations.Topology.Advection.UnitTests;

[TestFixture]
public sealed class GridAdvectionTests {

	[Test]
	public void Update_SourceWithoutHalo_Throws() {
		IGridAdvection advection = new GridAdvection( new SimulationClock() );
		Mock<IGridLayer<float>> source = new Mock<IGridLayer<float>>();
		source.Setup( l => l.Halo ).Returns( 0 );
		AdvectionNeighbourhood neighbourhood = new AdvectionNeighbourhood( Mock.Of<IGridLayer<Direction>>(), Mock.Of<IGridLayer<Direction>>() );

		Assert.Throws<ArgumentOutOfRangeException>( () => advection.Update(
			neighbourhood,
			Mock.Of<IGridLayer<float>>(),
			Mock.Of<IGridLayer<float>>(),
			source.Object,
			Mock.Of<IGridLayer<float>>()
		) );
		source.Verify( l => l.ExchangeHalos(), Times.Never );
	}

}
