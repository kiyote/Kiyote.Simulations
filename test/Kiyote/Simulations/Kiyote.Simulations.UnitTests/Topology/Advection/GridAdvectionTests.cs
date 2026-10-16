using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Moq;

namespace Kiyote.Simulations.Topology.Advection.UnitTests;

[TestFixture]
public sealed class GridAdvectionTests {

	private const int ChunkSize = 32;

	private IGridAdvection _advection;

	[SetUp]
	public void SetUp() {
		_advection = new GridAdvection( new SimulationClock() );
	}

	[Test]
	public void CreateNeighbourhood_PassableIsConnectivity_VacuumFromCompiled() {
		IGridLayer<Direction> connectivity = Mock.Of<IGridLayer<Direction>>();
		IGridLayer<Direction> vacuum = Mock.Of<IGridLayer<Direction>>();
		Mock<ICompiledGridAssembly<float>> compiled = new Mock<ICompiledGridAssembly<float>>( MockBehavior.Strict );
		compiled.Setup( c => c.CreateVacuumLayer() ).Returns( vacuum );

		AdvectionNeighbourhood result = _advection.CreateNeighbourhood( compiled.Object, connectivity );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( result.Passable, Is.SameAs( connectivity ) );
			Assert.That( result.Vacuum, Is.SameAs( vacuum ) );
		}
	}

	[Test]
	public void Update_StillVelocity_CopiesSourceAndMarksDestinationDirty() {
		ChunkedGridLayout layout = new ChunkedGridLayout( ChunkSize );
		for( int row = 0; row < ChunkSize; row++ ) {
			layout.SetPopulatedRun( 0, row, ChunkSize );
		}
		int chunk = layout.GetOrCreateChunk( 0, 0 );
		Mock<IGridLayer<float>> destination = Layer<float>( layout );
		Mock<IGridLayer<float>> source = Layer<float>( layout );
		Span<float> values = source.Object.Values.GetValues( chunk );
		for( int i = 0; i < values.Length; i++ ) {
			values[i] = i;
		}
		AdvectionNeighbourhood neighbourhood = new AdvectionNeighbourhood( Layer<Direction>( layout ).Object, Layer<Direction>( layout ).Object );

		_advection.Update( neighbourhood, Layer<float>( layout ).Object, Layer<float>( layout ).Object, source.Object, destination.Object );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( destination.Object.Values.GetValues( chunk ).ToArray(), Is.EqualTo( values.ToArray() ) );
			destination.Verify( l => l.MarkDirty( chunk ), Times.Once );
		}
	}

	[Test]
	public void Update_EmptyChunk_Skipped() {
		ChunkedGridLayout layout = new ChunkedGridLayout( ChunkSize );
		int chunk = layout.GetOrCreateChunk( 0, 0 );
		Mock<IGridLayer<float>> destination = Layer<float>( layout );
		AdvectionNeighbourhood neighbourhood = new AdvectionNeighbourhood( Layer<Direction>( layout ).Object, Layer<Direction>( layout ).Object );

		_advection.Update( neighbourhood, Layer<float>( layout ).Object, Layer<float>( layout ).Object, Layer<float>( layout ).Object, destination.Object );

		destination.Verify( l => l.MarkDirty( chunk ), Times.Never );
	}

	private static Mock<IGridLayer<T>> Layer<T>(
		ChunkedGridLayout layout
	) where T : unmanaged {
		Mock<IGridLayer<T>> layer = new Mock<IGridLayer<T>>();
		layer.Setup( l => l.Layout ).Returns( layout );
		layer.Setup( l => l.Values ).Returns( new ChunkedGridLayer<T>( layout ) );
		return layer;
	}

}
