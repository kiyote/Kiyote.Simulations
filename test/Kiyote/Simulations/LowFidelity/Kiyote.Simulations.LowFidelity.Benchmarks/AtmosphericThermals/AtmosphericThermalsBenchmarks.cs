namespace Kiyote.Simulations.LowFidelity.Benchmarks.AtmosphericThermals;

[MemoryDiagnoser( false )]
public class AtmosphericThermalsBenchmarks {

	private readonly CoupledBenchmarkSimulation _simulation = new CoupledBenchmarkSimulation();

	[Params( 1, 8 )]
	public int TicksPerUpdate { get; set; }

	[GlobalSetup]
	public void WarmupAndValidateAllocations() => _simulation.WarmupAndValidateAllocations( TicksPerUpdate );

	[Benchmark]
	public int Update() => _simulation.Update( TicksPerUpdate );

	[GlobalCleanup]
	public void Cleanup() => _simulation.Cleanup();

}
