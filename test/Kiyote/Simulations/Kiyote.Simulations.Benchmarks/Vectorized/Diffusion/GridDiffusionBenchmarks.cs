using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Diffusion;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Diffusion;

[MemoryDiagnoser( false )]
public class GridDiffusionBenchmarks {

	private readonly IGridDiffusion _diffusion;
	private Field<float> _input;
	private Field<float> _output;
	private readonly FloatConnectivityStrategy _connectivityStrategy;
	private readonly IConnectivityGrid<float> _connectivity;

	public GridDiffusionBenchmarks() {
		_diffusion = new GridDiffusion( new Settings( 0.1f ) );
		IMutableGrid<float> grid = new RaggedArrayGrid<float>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			grid[0, r] = 100f;
		}

		_connectivityStrategy = new FloatConnectivityStrategy();
		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( grid, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );

		GridTopology<float> topology = _connectivity.BuildTopology();
		IFieldCompiler compiler = new FieldCompiler();
		_input = compiler.Compile<float, Identity>( topology, default );
		_output = new Field<float>( topology );
	}

	[Benchmark]
	public void Update() {
		_diffusion.Update( _input, _output );
		(_input, _output) = (_output, _input);
	}

	private readonly struct Identity : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}

	private sealed class Settings : IGridDiffusionSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}
}
