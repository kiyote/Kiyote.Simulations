using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

/*
ManualConfig config = DefaultConfig.Instance
	.AddExporter( MarkdownExporter.Default )
	.AddJob( Job
		 .MediumRun
		 .WithLaunchCount( 1 )
		 .WithToolchain( InProcessNoEmitToolchain.Instance ) );

BenchmarkSwitcher
	.FromTypes( [
		typeof( SealedAnalyzerBenchmarks ),
		typeof( GridDiffusionBenchmarks ),
		typeof( GridFluidBenchmarks ),
		typeof( GridPressureBenchmarks ),
		typeof( GridAirflowBenchmarks )
	] )
	.RunAll( config, args );


*/

Console.WriteLine( "Hello world." );
