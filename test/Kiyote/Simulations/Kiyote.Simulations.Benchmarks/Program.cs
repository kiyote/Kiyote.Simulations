using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

ManualConfig config = DefaultConfig.Instance
	.AddExporter( MarkdownExporter.Default )
	.AddJob( Job
		 .MediumRun
		 .WithLaunchCount( 1 )
		 .WithToolchain( InProcessNoEmitToolchain.Instance ) );

BenchmarkSwitcher
	.FromTypes( [
		//typeof( Kiyote.Simulations.Benchmarks.Diffusion.GridDiffusionBenchmarks ),
		//typeof( Kiyote.Simulations.Benchmarks.Vectorized.Diffusion.GridDiffusionBenchmarks ),

		//typeof( Kiyote.Simulations.Benchmarks.Pressure.GridPressureBenchmarks ),
		//typeof( Kiyote.Simulations.Benchmarks.Vectorized.Pressure.GridPressureBenchmarks ),

		typeof( Kiyote.Simulations.Benchmarks.Projection.GridProjectionBenchmarks ),
		typeof( Kiyote.Simulations.Benchmarks.Vectorized.Projection.GridProjectionBenchmarks ),
	] )
	.RunAll( config, args );
