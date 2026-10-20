namespace Kiyote.Simulations.LowFidelity.Benchmarks.AtmosphericThermals;

[MemoryDiagnoser( false )]
public class ConvectionTopologyBenchmarks {

	private CoupledBenchmarkSimulation? _simulation;

	[Params( false, true )]
	public bool Sparse { get; set; }

	private CoupledBenchmarkSimulation Simulation => _simulation
		?? throw new InvalidOperationException( "The benchmark has not been initialized." );

	[GlobalSetup]
	public void Setup() {
		_simulation = new CoupledBenchmarkSimulation( size: 16, roomsPerSide: 2, spacing: Sparse ? 768 : 16 );
		_simulation.WarmupAndValidateAllocations( 1 );
	}

	[Benchmark]
	public int Update() => Simulation.Update( 1 );

	// Includes both children's metadata preparation and convection-link rebuilding, but no physics ticks.
	[Benchmark]
	public int RebuildTopology() => Simulation.RebuildTopology();

	[GlobalCleanup]
	public void Cleanup() => Simulation.Cleanup();

}
