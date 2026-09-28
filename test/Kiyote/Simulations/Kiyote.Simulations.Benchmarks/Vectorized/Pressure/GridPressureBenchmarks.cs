using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Benchmarks.Diffusion;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Pressure;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Pressure;

[MemoryDiagnoser( false )]
public class GridPressureBenchmarks {

	private readonly ISimulationClock _clock;
	private readonly IGridPressure _pressure;
	private Field<float> _input;
	private Field<float> _output;
	private readonly IConnectivityGrid<float> _connectivity;
	private readonly FloatConnectivityStrategy _connectivityStrategy;

	public GridPressureBenchmarks() {
		_clock = new SimulationClock();
		_pressure = new GridPressure( new Settings( 1f ), _clock );
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
		_pressure.Update( _input, _output );
		(_input, _output) = (_output, _input);
	}

	private readonly struct Identity : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}

	private sealed class Settings : IGridPressureSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}
}
