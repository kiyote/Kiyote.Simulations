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
				typeof( Diffusion.GridDiffusionBenchmarks ),
				typeof( Vectorized.Diffusion.GridDiffusionBenchmarks ),

				typeof( Pressure.GridPressureBenchmarks ),
				typeof( Vectorized.Pressure.GridPressureBenchmarks ),

				typeof( Projection.GridProjectionBenchmarks ),
				typeof( Vectorized.Projection.GridProjectionBenchmarks ),

				typeof( Advection.GridAdvectionBenchmarks ),
				typeof( Vectorized.Advection.GridAdvectionBenchmarks ),

				typeof( Airflow.GridAirflowBenchmarks ),
				typeof( Vectorized.Airflow.GridAirflowBenchmarks ),
			] )
			.RunAll( config, args );
	}

}
