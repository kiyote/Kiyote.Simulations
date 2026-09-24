using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Kiyote.Simulations.Benchmarks.Diffusion;
using Kiyote.Simulations.Benchmarks.Pressure;
using Kiyote.Simulations.Benchmarks.Projection;

ManualConfig config = DefaultConfig.Instance
	.AddExporter( MarkdownExporter.Default )
	.AddJob( Job
		 .MediumRun
		 .WithLaunchCount( 1 )
		 .WithToolchain( InProcessNoEmitToolchain.Instance ) );

BenchmarkSwitcher
	.FromTypes( [
		//typeof( GridDiffusionBenchmarks ),
		//typeof( GridPressureBenchmarks ),
		typeof( GridProjectionBenchmarks ),
	] )
	.RunAll( config, args );
