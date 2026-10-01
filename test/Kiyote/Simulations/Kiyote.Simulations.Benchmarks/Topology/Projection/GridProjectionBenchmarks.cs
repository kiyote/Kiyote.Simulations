using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Benchmarks.Topology.Projection;

[MemoryDiagnoser( false )]
public class GridProjectionBenchmarks {

	private const int Size = 100;
	private const int ChunkSize = 16;

	private readonly IGridProjection _projection;
	private readonly ICompiledGridAssembly<float> _compiled;
	private readonly ProjectionNeighbourhood _neighbourhood;
	private readonly IGridLayer<float> _velocityX;
	private readonly IGridLayer<float> _velocityY;
	private readonly IGridLayer<float> _velocityXScratch;
	private readonly IGridLayer<float> _velocityYScratch;
	private readonly IGridLayer<float> _pressure;
	private readonly IGridLayer<float> _pressureScratch;
	private readonly IGridLayer<float> _divergence;

	public GridProjectionBenchmarks() {
		_projection = new GridProjection( new Settings( 20 ) );
		DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, 0f );
			}
		}

		IGridAssembly<float> assembly = new GridAssembly<float>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}
		_compiled = new GridCompiler().Compile( assembly, ChunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open(), 0 );
		_neighbourhood = _projection.CreateNeighbourhood( _compiled, connectivity );
		_velocityX = _compiled.CreateLayer<float>( 1 );
		_velocityY = _compiled.CreateLayer<float>( 1 );
		_velocityXScratch = _compiled.CreateLayer<float>( 1 );
		_velocityYScratch = _compiled.CreateLayer<float>( 1 );
		_pressure = _compiled.CreateLayer<float>( 1 );
		_pressureScratch = _compiled.CreateLayer<float>( 1 );
		_divergence = _compiled.CreateLayer<float>( 0 );
		for( int row = 0; row < Size; row++ ) {
			_velocityX[0, row] = 100f;
		}
	}

	[Benchmark]
	public void Update() {
		_projection.Update( _neighbourhood, _velocityX, _velocityY, _velocityXScratch, _velocityYScratch, _pressure, _pressureScratch, _divergence );
		_compiled.Swap( _velocityX, _velocityXScratch );
		_compiled.Swap( _velocityY, _velocityYScratch );
	}

	[GlobalCleanup]
	public void Cleanup() {
		_compiled.Dispose();
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

	private sealed class Settings : IGridProjectionSettings {
		public Settings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}
}
