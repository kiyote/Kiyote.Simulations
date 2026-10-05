using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Benchmarks.Atmospherics;

// Mirrors Kiyote.Simulations.Benchmarks.Topology.Airflow.GridAirflowBenchmarks:
// a fully open 100x100 grid seeded at (50,50), one simulation step per Update.
[MemoryDiagnoser( false )]
public class AtmosphereBenchmarks {

	private const int Size = 100;
	private const float FixedTimeStep = 0.1f;

	private static readonly TimeSpan Step = TimeSpan.FromSeconds( FixedTimeStep );

	private readonly IAtmosphere _atmosphere;
	private readonly GasIndex _gas;

	public AtmosphereBenchmarks() {
		IGasRegistryBuilder gasRegistryBuilder = new GasRegistryBuilder( [new GasDefinitionSource()] );
		IGasRegistry gases = gasRegistryBuilder.Build();
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( gases )
			.AddSingleton<IAtmosphericsSettings, AtmosphericsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		IGridAtmospherics atmospherics = services.GetRequiredService<IGridAtmospherics>();

		DenseGridSource<Cell> grid = new DenseGridSource<Cell>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, new Cell( IsGasPermeable: true ) );
			}
		}
		IGridAssembly<Cell> assembly = new GridAssembly<Cell>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		_atmosphere = atmospherics.Create( assembly, new CellStrategy() );
		_gas = gases.GetIndex( "N2" );
		_atmosphere.AddGas( 50, 50, _gas, 1000f );
	}

	[Benchmark]
	public void Update() {
		// Keep a source running so chunks never settle and go to sleep,
		// keeping the work per step comparable to the always-on airflow benchmark.
		_atmosphere.AddGas( 50, 50, _gas, 1f );
		_ = _atmosphere.Advance( Step );
	}

	[GlobalCleanup]
	public void Cleanup() {
		_atmosphere.Dispose();
	}

	private readonly record struct Cell(
		bool IsGasPermeable
	);

	private readonly struct CellStrategy : IAtmosphereCellStrategy<Cell> {
		float IAtmosphereCellStrategy<Cell>.GetCondensate( in Cell cell, GasIndex gas ) => 0.0f;
		float IAtmosphereCellStrategy<Cell>.GetGas( in Cell cell, GasIndex gas ) => 0.0f;
		float IAtmosphereCellStrategy<Cell>.GetTemperature( in Cell cell ) => 293.15f;
		bool IAtmosphereCellStrategy<Cell>.IsPermeable( in Cell cell ) => cell.IsGasPermeable;
		void IAtmosphereCellStrategy<Cell>.SetCondensate( ref Cell cell, GasIndex gas, float amount ) { }
		void IAtmosphereCellStrategy<Cell>.SetGas( ref Cell cell, GasIndex gas, float amount ) { }
		void IAtmosphereCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) { }
	}

	private sealed class GasDefinitionSource : IGasDefinitionSource {
		IEnumerable<GasDefinition> IGasDefinitionSource.GetDefinitions() {
			yield return new GasDefinition( "O2", "Oxygen", 90.2f, 54.4f );
			yield return new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f );
		}
	}

	private sealed class AtmosphericsSettings : IAtmosphericsSettings {
		public float Acceleration => 2.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.1f;
		public float HeatCapacity => 20.8f;
		public float CondensationRate => 0.05f;
		public float FixedTimeStep => AtmosphereBenchmarks.FixedTimeStep;
		public int MaxStepsPerAdvance => 1;
		public float WindScale => 1.0f;
	}
}
