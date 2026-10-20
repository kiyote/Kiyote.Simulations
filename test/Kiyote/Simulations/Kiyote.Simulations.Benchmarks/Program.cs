using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Kiyote.Simulations.Benchmarks;

public static class Program {
	
	public static void Main(
		string[] args
	) {
		ManualConfig config = DefaultConfig.Instance
	.AddExporter( MarkdownExporter.Default )
	.AddJob( Job
		 .MediumRun
		 .WithLaunchCount( 1 )
		 .WithToolchain( InProcessNoEmitToolchain.Instance ) );

		BenchmarkSwitcher
			.FromTypes( [
				typeof( Topology.Diffusion.GridDiffusionBenchmarks ),
				typeof( Topology.Pressure.GridPressureBenchmarks ),
				typeof( Topology.Projection.GridProjectionBenchmarks ),
				typeof( Topology.Advection.GridAdvectionBenchmarks ),
				typeof( Topology.Airflow.GridAirflowBenchmarks ),
			] )
			.Run( args, config );
	}

}
