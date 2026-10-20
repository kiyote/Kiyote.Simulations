using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Airflow;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Benchmarks.Topology.Airflow;

internal sealed class AirflowBenchmarkSimulation {

	private const int Size = 100;
	private const int ChunkSize = 32;

	private readonly IGridAirflow _airflow;
	private readonly ICompiledGridAssembly<float> _compiled;
	private readonly AirflowNeighbourhood _neighbourhood;
	private readonly IGridLayer<float> _intermediateVelocityX;
	private readonly IGridLayer<float> _intermediateVelocityY;
	private readonly IGridLayer<float> _projectionPressure;
	private readonly IGridLayer<float> _projectionPressureScratch;
	private readonly IGridLayer<float> _projectionDivergence;
	private readonly IGridLayer<float> _inputPressure;
	private readonly IGridLayer<float> _outputPressure;
	private readonly IGridLayer<float> _inputVelocityX;
	private readonly IGridLayer<float> _inputVelocityY;
	private readonly IGridLayer<float> _outputVelocityX;
	private readonly IGridLayer<float> _outputVelocityY;
	private readonly IGridLayer<float> _inputConcentration;
	private readonly IGridLayer<float> _outputConcentration;

	public AirflowBenchmarkSimulation( int iterations, int retainedEmptyChunks ) {
		ISimulationClock clock = new SimulationClock();
		_airflow = new GridAirflow(
			new GridPressure( new PressureSettings( 1f ), clock ),
			new GridProjection( new ProjectionSettings( iterations ) ),
			new GridAdvection( clock ),
			clock
		);

		_compiled = BenchmarkGridAssembly.Create( Size, ChunkSize, retainedEmptyChunks );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open() );
		_neighbourhood = _airflow.CreateNeighbourhood( _compiled, connectivity );
		_intermediateVelocityX = _compiled.CreateLayer<float>();
		_intermediateVelocityY = _compiled.CreateLayer<float>();
		_projectionPressure = _compiled.CreateLayer<float>();
		_projectionPressureScratch = _compiled.CreateLayer<float>();
		_projectionDivergence = _compiled.CreateLayer<float>();
		_inputPressure = _compiled.CreateLayer<float>();
		_outputPressure = _compiled.CreateLayer<float>();
		_inputVelocityX = _compiled.CreateLayer<float>();
		_inputVelocityY = _compiled.CreateLayer<float>();
		_outputVelocityX = _compiled.CreateLayer<float>();
		_outputVelocityY = _compiled.CreateLayer<float>();
		_inputConcentration = _compiled.CreateLayer<float>();
		_outputConcentration = _compiled.CreateLayer<float>();

		_inputPressure[50, 50] = 1000f;
		_inputVelocityX[50, 50] = 40f;
		_inputConcentration[50, 50] = 1000f;
	}

	public void Update() {
		_airflow.Update(
			_compiled,
			_neighbourhood,
			_inputPressure, _outputPressure,
			_inputVelocityX, _inputVelocityY,
			_outputVelocityX, _outputVelocityY,
			_intermediateVelocityX, _intermediateVelocityY,
			_projectionPressure, _projectionPressureScratch, _projectionDivergence,
			_inputConcentration, _outputConcentration
		);
		_compiled.Swap( _inputPressure, _outputPressure );
		_compiled.Swap( _inputVelocityX, _outputVelocityX );
		_compiled.Swap( _inputVelocityY, _outputVelocityY );
		_compiled.Swap( _inputConcentration, _outputConcentration );
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
			throw new InvalidOperationException( $"Warmed airflow updates allocated {allocated} managed bytes." );
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
