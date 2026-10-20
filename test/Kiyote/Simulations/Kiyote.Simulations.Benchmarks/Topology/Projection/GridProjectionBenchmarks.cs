namespace Kiyote.Simulations.Benchmarks.Topology.Projection;

[MemoryDiagnoser( false )]
public class GridProjectionBenchmarks {

	private ProjectionBenchmarkSimulation? _simulation;

	[Params( 1, 20, 21 )]
	public int Iterations { get; set; }

	[Params( 0, 128 )]
	public int RetainedEmptyChunks { get; set; }

	private ProjectionBenchmarkSimulation Simulation => _simulation
		?? throw new InvalidOperationException( "The benchmark has not been initialized." );

	[GlobalSetup]
	public void Setup() {
		_simulation = new ProjectionBenchmarkSimulation( Iterations, RetainedEmptyChunks );
		_simulation.WarmupAndValidateAllocations();
	}

	[Benchmark]
	public void Update() => Simulation.Update();

	[GlobalCleanup]
	public void Cleanup() => Simulation.Cleanup();

}
