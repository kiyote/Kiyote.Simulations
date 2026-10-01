using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;

namespace Kiyote.Simulations.Benchmarks.Topology.Advection;

[MemoryDiagnoser( false )]
public class GridAdvectionBenchmarks {

	private const int Size = 100;
	private const int ChunkSize = 16;

	private readonly IGridAdvection _advection;
	private readonly ICompiledGridAssembly<float> _compiled;
	private readonly AdvectionNeighbourhood _neighbourhood;
	private readonly IGridLayer<float> _velocityX;
	private readonly IGridLayer<float> _velocityY;
	private readonly IGridLayer<float> _values;
	private readonly IGridLayer<float> _scratch;

	public GridAdvectionBenchmarks() {
		_advection = new GridAdvection( new SimulationClock() );
		DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, column == 0 ? 100f : 0f );
			}
		}

		IGridAssembly<float> assembly = new GridAssembly<float>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}
		_compiled = new GridCompiler().Compile( assembly, ChunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open(), 0 );
		_neighbourhood = _advection.CreateNeighbourhood( _compiled, connectivity );
		_values = _compiled.Bind<float, Identity>( default, 1 );
		_scratch = _compiled.CreateLayer<float>( 1 );
		_velocityX = _compiled.CreateLayer<float>( 0 );
		_velocityY = _compiled.CreateLayer<float>( 0 );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				_velocityX[column, row] = 5f;
				_velocityY[column, row] = 2f;
			}
		}
	}

	[Benchmark]
	public void Update() {
		_advection.Update( _neighbourhood, _velocityX, _velocityY, _values, _scratch );
		_compiled.Swap( _values, _scratch );
	}

	[GlobalCleanup]
	public void Cleanup() {
		_compiled.Dispose();
	}

	private readonly struct Identity : IGridLayerBinding<float, float> {
		float IGridLayerBinding<float, float>.Extract( in float cell ) => cell;
		void IGridLayerBinding<float, float>.Commit( ref float cell, float value ) => cell = value;
	}

	private readonly struct Open : IConnectivityStrategy<float> {
		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied;
	}
}
