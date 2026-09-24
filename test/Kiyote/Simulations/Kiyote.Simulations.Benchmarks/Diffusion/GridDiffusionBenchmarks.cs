using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Benchmarks.Diffusion;

[MemoryDiagnoser( false )]
public class GridDiffusionBenchmarks {

	private readonly IGridDiffusion _diffusion;
	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;
	private readonly FloatConnectivityStrategy _connectivityStrategy;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatDiffusionStrategy _diffusionStrategy;


	public GridDiffusionBenchmarks() {
		_diffusionStrategy = new FloatDiffusionStrategy();
		_diffusion = new GridDiffusion();
		_input = new ArrayGrid<float>( 100, 100 );
		_output = new ArrayGrid<float>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			_input[0, r] = 100f;
		}

		_connectivityStrategy = new FloatConnectivityStrategy();
		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( _input, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );
	}

	[Benchmark]
	public void Update() {
		_diffusion.Update<float, float, float, FloatDiffusionStrategy>( _connectivity, _input, _output, _diffusionStrategy );
		(_input, _output) = (_output, _input);
	}
}
