using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;

namespace Kiyote.Simulations.Benchmarks.Vectorized.Advection;

[MemoryDiagnoser( false )]
public class GridAdvectionBenchmarks {

	private readonly IGridAdvection _advection;
	private readonly AdvectionNeighbourhood<float> _neighbourhood;
	private readonly Field<float> _velocityX;
	private readonly Field<float> _velocityY;
	private Field<float> _input;
	private Field<float> _output;

	public GridAdvectionBenchmarks() {
		_advection = new GridAdvection( new SimulationClock() );
		IMutableGrid<float> grid = new RaggedArrayGrid<float>( 100, 100 );
		for( int r = 0; r < 100; r++ ) {
			grid[0, r] = 100f;
		}

		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( grid, 0, 0 );
		connectivity.UpdateConnectivity( new FloatConnectivityStrategy() );

		GridTopology<float> topology = connectivity.BuildTopology();
		IFieldCompiler compiler = new FieldCompiler();
		_neighbourhood = new AdvectionNeighbourhood<float>( topology );
		_input = compiler.Compile<float, Identity>( topology, default );
		_output = new Field<float>( topology );
		_velocityX = new Field<float>( topology );
		_velocityY = new Field<float>( topology );
		_velocityX.Values.Fill( 5f );
		_velocityY.Values.Fill( 2f );
	}

	[Benchmark]
	public void Update() {
		_advection.Update( _neighbourhood, _velocityX, _velocityY, _input, _output );
		(_input, _output) = (_output, _input);
	}

	private readonly struct Identity : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}
}
