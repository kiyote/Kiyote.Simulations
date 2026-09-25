using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.Pressure;

namespace Kiyote.Simulations.Benchmarks.Pressure;

[MemoryDiagnoser(false)]
public class GridPressureBenchmarks {

	private readonly ISimulationClock _clock;
	private readonly IGridDiffusion _diffusion;
	private readonly IGridPressure _pressure;
	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;
	private readonly FloatPressureStrategy _pressureStrategy;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatConnectivityStrategy _connectivityStrategy;


	public GridPressureBenchmarks() {
		_clock = new SimulationClock();
		_diffusion = new GridDiffusion();
		_pressureStrategy = new FloatPressureStrategy();
		_pressure = new GridPressure( _diffusion, _clock );
		_input = new RaggedArrayGrid<float>( 100, 100 );
		_output = new RaggedArrayGrid<float>( 100, 100 );
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
		_pressure.Update<float, float, float, FloatPressureStrategy>( _connectivity, _input, _output, _pressureStrategy );
		(_input, _output) = (_output, _input);
	}
}
