using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Advection;

namespace Kiyote.Simulations.Benchmarks.Advection;

[MemoryDiagnoser( false )]
public class GridAdvectionBenchmarks {

	private readonly IGridAdvection _advection;
	private readonly FloatBilinearSampler _sampler;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly IMutableGrid<Velocity> _velocity;
	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;

	public GridAdvectionBenchmarks() {
		_advection = new GridAdvection( new SimulationClock() );
		_sampler = new FloatBilinearSampler();
		_input = new RaggedArrayGrid<float>( 100, 100 );
		_output = new RaggedArrayGrid<float>( 100, 100 );
		_velocity = new RaggedArrayGrid<Velocity>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			_input[0, r] = 100f;
			for( int c = 0; c < 100; c++ ) {
				_velocity[c, r] = new Velocity( 5f, 2f );
			}
		}

		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( _input, 0, 0 );
		_connectivity.UpdateConnectivity( new FloatConnectivityStrategy() );
	}

	[Benchmark]
	public void Update() {
		_advection.Update( _connectivity, _velocity, _input, _output, _sampler );
		(_input, _output) = (_output, _input);
	}
}
