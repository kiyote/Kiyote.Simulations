namespace Kiyote.Simulations.Benchmarks.Topology.Airflow;

[MemoryDiagnoser( false )]
public class GridAirflowBenchmarks {

	private AirflowBenchmarkSimulation? _simulation;

	[Params( 1, 20, 21 )]
	public int Iterations { get; set; }

	[Params( 0, 128 )]
	public int RetainedEmptyChunks { get; set; }

	private AirflowBenchmarkSimulation Simulation => _simulation
		?? throw new InvalidOperationException( "The benchmark has not been initialized." );

	[GlobalSetup]
	public void Setup() {
		_simulation = new AirflowBenchmarkSimulation( Iterations, RetainedEmptyChunks );
		_simulation.WarmupAndValidateAllocations();
	}

	[Benchmark]
	public void Update() => Simulation.Update();

	[GlobalCleanup]
	public void Cleanup() => Simulation.Cleanup();

}
