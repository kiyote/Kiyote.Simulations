using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Diffusion;

namespace Kiyote.Simulations.Benchmarks.Topology.Diffusion;

[MemoryDiagnoser( false )]
public class GridDiffusionBenchmarks {

	private const int Size = 100;
	private const int ChunkSize = 16;

	private readonly IGridDiffusion _diffusion;
	private readonly ICompiledGridAssembly<float> _compiled;
	private readonly IGridLayer<Direction> _neighbourhood;
	private readonly IGridLayer<float> _values;
	private readonly IGridLayer<float> _scratch;

	public GridDiffusionBenchmarks() {
		_diffusion = new GridDiffusion( new Settings( 0.1f ) );
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
		_neighbourhood = _diffusion.CreateNeighbourhood( _compiled, connectivity );
		_values = _compiled.Bind<float, Identity>( default, 1 );
		_scratch = _compiled.CreateLayer<float>( 1 );
	}

	[Benchmark]
	public void Update() {
		_diffusion.Update( _neighbourhood, _values, _scratch );
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

	private sealed class Settings : IGridDiffusionSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}
}
