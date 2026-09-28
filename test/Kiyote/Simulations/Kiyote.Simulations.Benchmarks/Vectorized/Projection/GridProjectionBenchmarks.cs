using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Benchmarks.Diffusion;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Projection;

[MemoryDiagnoser( false )]
public class GridProjectionBenchmarks {

	private readonly IGridProjection _projection;
	private Field<float> _inputX;
	private Field<float> _inputY;
	private Field<float> _outputX;
	private Field<float> _outputY;
	private readonly Field<float> _pressure;
	private readonly Field<float> _pressureScratch;
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
		IFieldCompiler compiler = new FieldCompiler();
		_pressure = compiler.Compile<float, Identity>( topology, default );
		_pressureScratch = new Field<float>( topology );
		_inputX = new Field<float>( topology );
		_inputY = new Field<float>( topology );
		_outputX = new Field<float>( topology );
		_outputY = new Field<float>( topology );
		for( int r = 0; r < 100; r++ ) {
			_inputX.Values[r * 100] = 100f;
		}
	}

	[Benchmark]
	public void Update() {
		_projection.Update( _inputX, _inputY, _outputX, _outputY, _pressure, _pressureScratch );
		(_inputX, _outputX) = (_outputX, _inputX);
		(_inputY, _outputY) = (_outputY, _inputY);
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
