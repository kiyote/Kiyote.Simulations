using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Projection;

[MemoryDiagnoser( false )]
public class GridProjectionBenchmarks {

	private readonly IGridProjection _projection;
	private readonly ProjectionNeighbourhood<float> _neighbourhood;
	private Field<float> _inputVelocityX;
	private Field<float> _inputVelocityY;
	private Field<float> _outputVelocityX;
	private Field<float> _outputVelocityY;
	private readonly Field<float> _pressure;
	private readonly Field<float> _pressureScratch;
	private readonly Field<float> _divergence;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatConnectivityStrategy _connectivityStrategy;

	public GridProjectionBenchmarks() {
		_projection = new GridProjection( new Settings( 20 ) );
		IMutableGrid<float> grid = new RaggedArrayGrid<float>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			grid[0, r] = 100f;
		}

		_connectivityStrategy = new FloatConnectivityStrategy();
		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( grid, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );

		GridTopology<float> topology = _connectivity.BuildTopology();
		_neighbourhood = new ProjectionNeighbourhood<float>( topology );
		IFieldCompiler compiler = new FieldCompiler();
		_pressure = compiler.Compile<float, Identity>( topology, default );
		_pressureScratch = new Field<float>( topology );
		_divergence = new Field<float>( topology );
		_inputVelocityX = new Field<float>( topology );
		_inputVelocityY = new Field<float>( topology );
		_outputVelocityX = new Field<float>( topology );
		_outputVelocityY = new Field<float>( topology );
		for( int r = 0; r < 100; r++ ) {
			_inputVelocityX.Values[r * 100] = 100f;
		}
	}

	[Benchmark]
	public void Update() {
		_projection.Update( _neighbourhood, _inputVelocityX, _inputVelocityY, _outputVelocityX, _outputVelocityY, _pressure, _pressureScratch, _divergence );
		(_inputVelocityX, _outputVelocityX) = (_outputVelocityX, _inputVelocityX);
		(_inputVelocityY, _outputVelocityY) = (_outputVelocityY, _inputVelocityY);
	}

	private readonly struct Identity : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}

	private sealed class Settings : IGridProjectionSettings {
		public Settings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}
}
