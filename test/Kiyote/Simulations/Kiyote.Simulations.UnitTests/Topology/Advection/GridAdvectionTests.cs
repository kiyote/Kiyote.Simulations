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
		using ICompiledGridAssembly<float> compiled = Compile( full: true );
		Assert.That( compiled.ChunkLayout.TryGetChunk( 0, 0, out int chunk ), Is.True );
		IGridLayer<float> destination = compiled.CreateLayer<float>();
		IGridLayer<float> source = compiled.CreateLayer<float>();
		Span<float> values = source.GetValues( chunk );
		for( int i = 0; i < values.Length; i++ ) {
			values[i] = i;
		}
		AdvectionNeighbourhood neighbourhood = new AdvectionNeighbourhood( compiled.CreateLayer<Direction>(), compiled.CreateLayer<Direction>() );

		_advection.Update( neighbourhood, compiled.CreateLayer<float>(), compiled.CreateLayer<float>(), source, destination );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( destination.GetValues( chunk ).ToArray(), Is.EqualTo( values.ToArray() ) );
			Assert.That( destination.IsDirty( chunk ), Is.True );
		}
	}

	[Test]
	public void Update_EmptyChunk_Skipped() {
		using ICompiledGridAssembly<float> compiled = Compile( full: false );
		Assert.That( compiled.ChunkLayout.TryGetChunk( 0, 0, out int chunk ), Is.True );
		Assert.That( compiled.Assembly.TryRemoveCell( compiled.Assembly.Placements[0].Id, 0, 0 ), Is.True );
		IGridLayer<float> destination = compiled.CreateLayer<float>();
		AdvectionNeighbourhood neighbourhood = new AdvectionNeighbourhood( compiled.CreateLayer<Direction>(), compiled.CreateLayer<Direction>() );

		_advection.Update( neighbourhood, compiled.CreateLayer<float>(), compiled.CreateLayer<float>(), compiled.CreateLayer<float>(), destination );

		Assert.That( destination.IsDirty( chunk ), Is.False );
	}

	private static ICompiledGridAssembly<float> Compile(
		bool full
	) {
		DenseGridSource<float> source = new DenseGridSource<float>( ChunkSize, ChunkSize );
		int size = full ? ChunkSize : 1;
		for( int row = 0; row < size; row++ ) {
			for( int column = 0; column < size; column++ ) {
				_ = source.TrySetCell( column, row, 0f );
			}
		}
		IGridAssembly<float> assembly = new GridAssembly<float>();
		Assert.That( assembly.TryAttach( source, 0, 0 ).Succeeded, Is.True );
		return new GridCompiler().Compile( assembly, ChunkSize );
	}

}
