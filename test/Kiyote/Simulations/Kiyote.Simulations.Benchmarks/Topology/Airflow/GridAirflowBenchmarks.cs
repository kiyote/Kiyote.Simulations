using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Airflow;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Benchmarks.Topology.Airflow;

[MemoryDiagnoser( false )]
public class GridAirflowBenchmarks {

	private const int Size = 100;
	private const int ChunkSize = 16;

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

	public GridAirflowBenchmarks() {
		ISimulationClock clock = new SimulationClock();
		_airflow = new GridAirflow(
			new GridPressure( new PressureSettings( 1f ), clock ),
			new GridProjection( new ProjectionSettings( 20 ) ),
			new GridAdvection( clock ),
			clock
		);

		DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, 0f );
			}
		}
		IGridAssembly<float> assembly = new GridAssembly<float>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}
		_compiled = new GridCompiler().Compile( assembly, ChunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( _compiled, new Open(), 0 );
		_neighbourhood = _airflow.CreateNeighbourhood( _compiled, connectivity );
		_intermediateVelocityX = _compiled.CreateLayer<float>( 1 );
		_intermediateVelocityY = _compiled.CreateLayer<float>( 1 );
		_projectionPressure = _compiled.CreateLayer<float>( 1 );
		_projectionPressureScratch = _compiled.CreateLayer<float>( 1 );
		_projectionDivergence = _compiled.CreateLayer<float>( 0 );
		_inputPressure = _compiled.CreateLayer<float>( 1 );
		_outputPressure = _compiled.CreateLayer<float>( 1 );
		_inputVelocityX = _compiled.CreateLayer<float>( 1 );
		_inputVelocityY = _compiled.CreateLayer<float>( 1 );
		_outputVelocityX = _compiled.CreateLayer<float>( 1 );
		_outputVelocityY = _compiled.CreateLayer<float>( 1 );
		_inputConcentration = _compiled.CreateLayer<float>( 1 );
		_outputConcentration = _compiled.CreateLayer<float>( 1 );

		_inputPressure[50, 50] = 1000f;
		_inputVelocityX[50, 50] = 40f;
		_inputConcentration[50, 50] = 1000f;
	}

	[Benchmark]
	public void Update() {
		_airflow.Update(
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

	[GlobalCleanup]
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
