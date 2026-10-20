using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Benchmarks.Topology.Projection;

internal sealed class ProjectionBenchmarkSimulation {

	private const int Size = 100;
	private const int ChunkSize = 32;

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

	public ProjectionBenchmarkSimulation( int iterations, int retainedEmptyChunks ) {
		_projection = new GridProjection( new Settings( iterations ) );
		_compiled = BenchmarkGridAssembly.Create( Size, ChunkSize, retainedEmptyChunks );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open() );
		_neighbourhood = _projection.CreateNeighbourhood( _compiled, connectivity );
		_velocityX = _compiled.CreateLayer<float>();
		_velocityY = _compiled.CreateLayer<float>();
		_velocityXScratch = _compiled.CreateLayer<float>();
		_velocityYScratch = _compiled.CreateLayer<float>();
		_pressure = _compiled.CreateLayer<float>();
		_pressureScratch = _compiled.CreateLayer<float>();
		_divergence = _compiled.CreateLayer<float>();
		for( int row = 0; row < Size; row++ ) {
			_velocityX[0, row] = 100f;
		}
	}

	public void Update() {
		_projection.Update( _compiled, _neighbourhood, _velocityX, _velocityY, _velocityXScratch, _velocityYScratch, _pressure, _pressureScratch, _divergence );
		_compiled.Swap( _velocityX, _velocityXScratch );
		_compiled.Swap( _velocityY, _velocityYScratch );
	}

	public void WarmupAndValidateAllocations() {
		for( int i = 0; i < 100; i++ ) {
			Update();
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			Update();
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		if( allocated != 0 ) {
			throw new InvalidOperationException( $"Warmed projection updates allocated {allocated} managed bytes." );
		}
	}

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
