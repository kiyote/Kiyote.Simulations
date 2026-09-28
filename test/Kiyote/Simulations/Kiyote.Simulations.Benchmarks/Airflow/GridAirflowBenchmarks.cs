using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Advection;
using Kiyote.Simulations.Airflow;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Benchmarks.Airflow;

[MemoryDiagnoser( false )]
public class GridAirflowBenchmarks {

	private readonly IGridAirflow _airflow;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatProjectionStrategy _projectionStrategy;
	private readonly FloatBilinearSampler _sampler;
	private readonly IMutableGrid<float> _projectionPressureSource;
	private readonly IMutableGrid<float> _projectionPressureDestination;
	private readonly IMutableGrid<float> _projectionDivergence;
	private IMutableGrid<float> _inputPressure;
	private IMutableGrid<float> _outputPressure;
	private IMutableGrid<Velocity> _inputVelocity;
	private IMutableGrid<Velocity> _outputVelocity;
	private IMutableGrid<float> _inputConcentration;
	private IMutableGrid<float> _outputConcentration;

	public GridAirflowBenchmarks() {
		ISimulationClock clock = new SimulationClock();
		_airflow = new GridAirflow(
			new Kiyote.Simulations.Pressure.GridPressure( new Kiyote.Simulations.Diffusion.GridDiffusion(), clock ),
			new GridProjection(),
			new GridAdvection( clock ),
			clock
		);
		_projectionStrategy = new FloatProjectionStrategy();
		_sampler = new FloatBilinearSampler();
		_inputPressure = new RaggedArrayGrid<float>( 100, 100 );
		_outputPressure = new RaggedArrayGrid<float>( 100, 100 );
		_projectionPressureSource = new RaggedArrayGrid<float>( 100, 100 );
		_projectionPressureDestination = new RaggedArrayGrid<float>( 100, 100 );
		_projectionDivergence = new RaggedArrayGrid<float>( 100, 100 );
		_inputVelocity = new RaggedArrayGrid<Velocity>( 100, 100 );
		_outputVelocity = new RaggedArrayGrid<Velocity>( 100, 100 );
		_inputConcentration = new RaggedArrayGrid<float>( 100, 100 );
		_outputConcentration = new RaggedArrayGrid<float>( 100, 100 );

		_inputPressure[50, 50] = 1000f;
		_inputVelocity[50, 50] = new Velocity( 40f, 0f );
		_inputConcentration[50, 50] = 1000f;

		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( _inputPressure, 0, 0 );
		_connectivity.UpdateConnectivity( new FloatConnectivityStrategy() );
	}

	[Benchmark]
	public void Update() {
		_airflow.Update<float, float, float, float, FloatProjectionStrategy, FloatProjectionStrategy, FloatBilinearSampler>(
			_connectivity,
			_inputPressure,
			_outputPressure,
			_projectionStrategy,
			_inputVelocity,
			_outputVelocity,
			_projectionPressureSource,
			_projectionPressureDestination,
			_projectionDivergence,
			_projectionStrategy,
			_inputConcentration,
			_outputConcentration,
			_sampler
		);
		(_inputPressure, _outputPressure) = (_outputPressure, _inputPressure);
		(_inputVelocity, _outputVelocity) = (_outputVelocity, _inputVelocity);
		(_inputConcentration, _outputConcentration) = (_outputConcentration, _inputConcentration);
	}
}
