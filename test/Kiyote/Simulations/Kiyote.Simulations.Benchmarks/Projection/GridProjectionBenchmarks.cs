using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.Pressure;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Benchmarks.Projection;

[MemoryDiagnoser( false )]
public class GridProjectionBenchmarks {

	private readonly ISimulationClock _clock;
	private readonly IGridPressure _pressure;
	private readonly IGridDiffusion _diffusion;
	private readonly IGridProjection _projection;
	private IMutableGrid<float> _inputPressure;
	private IMutableGrid<float> _outputPressure;
	private IMutableGrid<Velocity> _inputVelocity;
	private IMutableGrid<Velocity> _outputVelocity;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatProjectionStrategy _projectionStrategy;


	public GridProjectionBenchmarks() {
		_connectivityStrategy = new FloatConnectivityStrategy();
		_projectionStrategy = new FloatProjectionStrategy();
		_clock = new SimulationClock();
		_diffusion = new GridDiffusion();
		_pressure = new GridPressure( _diffusion, _clock );
		_projection = new GridProjection( _pressure );
		_inputPressure = new ArrayGrid<float>( 100, 100 );
		_outputPressure = new ArrayGrid<float>( 100, 100 );
		_inputVelocity = new ArrayGrid<Velocity>( 100, 100 );
		_outputVelocity = new ArrayGrid<Velocity>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			_inputPressure[0, r] = 100f;
			_inputVelocity[0, r] = new Velocity( 100f, 0f );
		}

		_connectivityStrategy = new FloatConnectivityStrategy();
		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( _inputPressure, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );
	}

	[Benchmark]
	public void Update() {
		_projection.Update<float, float, FloatProjectionStrategy>( _connectivity, _inputVelocity, _outputVelocity, _inputPressure, _outputPressure, _projectionStrategy );
		(_inputPressure, _outputPressure) = (_outputPressure, _inputPressure);
		(_inputVelocity, _outputVelocity) = (_outputVelocity, _inputVelocity);
	}


}
