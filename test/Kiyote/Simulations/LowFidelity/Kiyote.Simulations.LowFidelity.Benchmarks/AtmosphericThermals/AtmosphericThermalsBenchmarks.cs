using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Benchmarks.AtmosphericThermals;

[MemoryDiagnoser( false )]
public class AtmosphericThermalsBenchmarks {

	private const int Size = 100;
	private readonly ServiceProvider _services;
	private readonly ICompiledGridAssembly<Cell> _compiled;
	private readonly IAtmosphericThermals _simulation;
	private readonly GasIndex _nitrogen;

	[Params( 1, 8 )]
	public int TicksPerUpdate { get; set; }

	public AtmosphericThermalsBenchmarks() {
		IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasSource()] ) ).Build();
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
		Settings settings = new Settings();
		_services = new ServiceCollection()
			.AddSingleton( gases )
			.AddSingleton( materials )
			.AddSingleton<IAtmosphericsSettings>( settings )
			.AddSingleton<IThermalsSettings>( settings )
			.AddSingleton<IAtmosphericThermalsSettings>( settings )
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		DenseGridSource<Cell> source = new DenseGridSource<Cell>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				bool wall = column == 0 || row == 0 || column == Size - 1 || row == Size - 1;
				if( !source.TrySetCell( column, row, new Cell( !wall ) ) ) {
					throw new InvalidOperationException( "Unable to create benchmark cell." );
				}
			}
		}
		IGridAssembly<Cell> assembly = new GridAssembly<Cell>();
		if( !assembly.TryAttach( source, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach benchmark source." );
		}
		_compiled = new GridCompiler().Compile( assembly, 32 );
		_nitrogen = gases.GetIndex( "N2" );
		Strategy strategy = new Strategy( _nitrogen, materials.GetIndex( "aluminium" ) );
		_simulation = _services.GetRequiredService<IGridAtmosphericThermals>().Create( _compiled, strategy, strategy );
		_simulation.Thermal.SunDirection = new Vector( 1.0f, -1.0f );
	}

	[GlobalSetup]
	public void WarmupAndValidateAllocations() {
		for( int i = 0; i < 100; i++ ) {
			_ = Update();
		}
		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			_ = Update();
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		if( allocated != 0 ) {
			throw new InvalidOperationException( $"Warmed coupled updates allocated {allocated} managed bytes." );
		}
	}

	// Each invocation advances exactly TicksPerUpdate fixed ticks and convection exchanges.
	[Benchmark]
	public int Update() {
		_simulation.Atmosphere.AddGas( 50, 50, _nitrogen, TicksPerUpdate, 300.0f );
		_simulation.Thermal.AddEnergy( 50, 50, 100000.0f * TicksPerUpdate );
		long previous = _simulation.Atmosphere.Frames.StepCount;
		int exchanges = _simulation.Update( TimeSpan.FromTicks( TimeSpan.TicksPerSecond / 10 * TicksPerUpdate ) );
		if( exchanges != TicksPerUpdate || _simulation.Atmosphere.Frames.StepCount - previous != TicksPerUpdate ) {
			throw new InvalidOperationException( "The benchmark did not advance the required ticks and exchanges." );
		}
		return exchanges;
	}

	[GlobalCleanup]
	public void Cleanup() {
		_simulation.Dispose();
		_compiled.Dispose();
		_services.Dispose();
	}

	private readonly record struct Cell( bool Permeable );

	private readonly record struct Strategy( GasIndex Nitrogen, MaterialIndex Material ) : IAtmosphereCellStrategy<Cell>, IThermalCellStrategy<Cell> {
		public bool IsPermeable( in Cell cell ) => cell.Permeable;
		public float GetGas( in Cell cell, GasIndex gas ) => cell.Permeable && gas == Nitrogen ? 1.0f : 0.0f;
		public void SetGas( ref Cell cell, GasIndex gas, float amount ) { }
		public float GetCondensate( in Cell cell, GasIndex gas ) => 0.0f;
		public void SetCondensate( ref Cell cell, GasIndex gas, float amount ) { }
		public float GetTemperature( in Cell cell ) => 300.0f;
		public void SetTemperature( ref Cell cell, float temperature ) { }
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => 293.15f;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) { }
		public MaterialIndex GetMaterial( in Cell cell ) => Material;
	}

	private sealed class GasSource : IGasDefinitionSource {
		public IEnumerable<GasDefinition> GetDefinitions() => [
			new GasDefinition( "O2", "Oxygen", 90.2f, 54.4f ),
			new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f )
		];
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() => [
			new MaterialDefinition( "aluminium", "Aluminium", Conductivity: 2.05f, HeatCapacity: 24200.0f, Emissivity: 0.1f, Absorptivity: 0.15f, Convection: 10.0f )
		];
	}

	private sealed class Settings : IAtmosphericsSettings, IThermalsSettings, IAtmosphericThermalsSettings {
		public float Acceleration => 2.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.1f;
		public float HeatCapacity => 20.8f;
		public float CondensationRate => 0.05f;
		public int MaxStepsPerUpdate => 8;
		public float WindScale => 1.0f;
		public float StefanBoltzmann => 5.67e-8f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 1361.0f;
		public float ExchangeInterval => 0.1f;
	}

}
