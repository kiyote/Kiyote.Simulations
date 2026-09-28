using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;
using Kiyote.Simulations.Vectorized.Airflow;
using Kiyote.Simulations.Vectorized.Pressure;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Airflow;

[MemoryDiagnoser( false )]
public class GridAirflowBenchmarks {

	private readonly IGridAirflow _airflow;
	private readonly AdvectionNeighbourhood<float> _neighbourhood;
	private readonly Field<float> _intermediateVelocityX;
	private readonly Field<float> _intermediateVelocityY;
	private readonly Field<float> _projectionPressure;
	private readonly Field<float> _projectionPressureScratch;
	private Field<float> _inputPressure;
	private Field<float> _outputPressure;
	private Field<float> _inputVelocityX;
	private Field<float> _inputVelocityY;
	private Field<float> _outputVelocityX;
	private Field<float> _outputVelocityY;
	private Field<float> _inputConcentration;
	private Field<float> _outputConcentration;

	public GridAirflowBenchmarks() {
		ISimulationClock clock = new SimulationClock();
		_airflow = new GridAirflow(
			new GridPressure( new PressureSettings( 1f ), clock ),
			new GridProjection( new ProjectionSettings( 20 ) ),
			new GridAdvection( clock ),
			clock
		);

		IMutableGrid<float> grid = new RaggedArrayGrid<float>( 100, 100 );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( grid, 0, 0 );
		connectivity.UpdateConnectivity( new FloatConnectivityStrategy() );
		GridTopology<float> topology = connectivity.BuildTopology();

		_neighbourhood = new AdvectionNeighbourhood<float>( topology );
		_intermediateVelocityX = new Field<float>( topology );
		_intermediateVelocityY = new Field<float>( topology );
		_projectionPressure = new Field<float>( topology );
		_projectionPressureScratch = new Field<float>( topology );
		_inputPressure = new Field<float>( topology );
		_outputPressure = new Field<float>( topology );
		_inputVelocityX = new Field<float>( topology );
		_inputVelocityY = new Field<float>( topology );
		_outputVelocityX = new Field<float>( topology );
		_outputVelocityY = new Field<float>( topology );
		_inputConcentration = new Field<float>( topology );
		_outputConcentration = new Field<float>( topology );

		int centre = ( 50 * 100 ) + 50;
		_inputPressure.Values[centre] = 1000f;
		_inputVelocityX.Values[centre] = 40f;
		_inputConcentration.Values[centre] = 1000f;
	}

	[Benchmark]
	public void Update() {
		_airflow.Update(
			_neighbourhood,
			_inputPressure, _outputPressure,
			_inputVelocityX, _inputVelocityY,
			_outputVelocityX, _outputVelocityY,
			_intermediateVelocityX, _intermediateVelocityY,
			_projectionPressure, _projectionPressureScratch,
			_inputConcentration, _outputConcentration
		);
		(_inputPressure, _outputPressure) = (_outputPressure, _inputPressure);
		(_inputVelocityX, _outputVelocityX) = (_outputVelocityX, _inputVelocityX);
		(_inputVelocityY, _outputVelocityY) = (_outputVelocityY, _inputVelocityY);
		(_inputConcentration, _outputConcentration) = (_outputConcentration, _inputConcentration);
	}

	private sealed class PressureSettings : IGridPressureSettings {
		public PressureSettings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

	private sealed class ProjectionSettings : IGridProjectionSettings {
		public ProjectionSettings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}
}
