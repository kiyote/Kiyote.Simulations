using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Kiyote.Simulations.LowFidelity.Benchmarks;

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
				typeof( Atmospherics.AtmosphereBenchmarks ),
				typeof( Thermals.ThermalBenchmarks ),
			] )
			.Run( args, config );
	}

}
