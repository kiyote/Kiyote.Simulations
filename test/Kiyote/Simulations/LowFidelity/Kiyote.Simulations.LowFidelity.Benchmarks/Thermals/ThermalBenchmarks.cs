using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Benchmarks.Thermals;

// Mirrors AtmosphereBenchmarks: a fully solid 100x100 aluminium plate with a hot spot at (50,50),
// one simulation step per Update. Conduction, radiation and solar heating all run every step.
[MemoryDiagnoser( false )]
public class ThermalBenchmarks {

	private const int Size = 100;
	private const float FixedTimeStep = 10.0f;

	private static readonly TimeSpan Step = TimeSpan.FromSeconds( FixedTimeStep );

	private readonly IThermal _thermal;
	private int _sunStep;

	public ThermalBenchmarks() {
		IMaterialRegistryBuilder materialRegistryBuilder = new MaterialRegistryBuilder( [new MaterialDefinitionSource()] );
		IMaterialRegistry materials = materialRegistryBuilder.Build();
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( materials )
			.AddSingleton<IThermalsSettings, ThermalsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		IGridThermals thermals = services.GetRequiredService<IGridThermals>();

		DenseGridSource<Cell> grid = new DenseGridSource<Cell>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, new Cell() );
			}
		}
		IGridAssembly<Cell> assembly = new GridAssembly<Cell>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		_thermal = thermals.Create( assembly, new CellStrategy( materials.GetIndex( "aluminium" ) ) );
		// Grid +Y is south, so north-east is (1, -1).
		_thermal.SunDirection = new Vector( 1.0f, -1.0f );
	}

	[Benchmark]
	public void Update() {
		// Keep a heat source running so the plate never settles.
		_thermal.AddEnergy( 50, 50, 100000.0f );
		_ = _thermal.Advance( Step );
	}

	[Benchmark]
	public void UpdateWithMovingSun() {
		// Changing the sun forces the solar exposure to be recomputed on the next step.
		_sunStep = ( _sunStep + 1 ) % 360;
		float angle = _sunStep * MathF.PI / 180.0f;
		_thermal.SunDirection = new Vector( MathF.Cos( angle ), MathF.Sin( angle ) );
		_thermal.AddEnergy( 50, 50, 100000.0f );
		_ = _thermal.Advance( Step );
	}

	[GlobalCleanup]
	public void Cleanup() {
		_thermal.Dispose();
	}

	private readonly record struct Cell;

	private readonly struct CellStrategy : IThermalCellStrategy<Cell> {

		private readonly MaterialIndex _material;

		public CellStrategy(
			MaterialIndex material
		) {
			_material = material;
		}

		MaterialIndex IThermalCellStrategy<Cell>.GetMaterial( in Cell cell ) => _material;
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => 293.15f;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) { }
	}

	private sealed class MaterialDefinitionSource : IMaterialDefinitionSource {
		IEnumerable<MaterialDefinition> IMaterialDefinitionSource.GetDefinitions() {
			yield return new MaterialDefinition( "aluminium", "Aluminium", Conductivity: 2.05f, HeatCapacity: 24200.0f, Emissivity: 0.1f, Absorptivity: 0.15f );
		}
	}

	private sealed class ThermalsSettings : IThermalsSettings {
		public float StefanBoltzmann => 5.67e-8f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 1361.0f;
		public float FixedTimeStep => ThermalBenchmarks.FixedTimeStep;
		public int MaxStepsPerAdvance => 1;
	}
}
