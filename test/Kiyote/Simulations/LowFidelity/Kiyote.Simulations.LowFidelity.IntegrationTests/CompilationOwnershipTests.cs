using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.IntegrationTests;

[TestFixture]
internal sealed class CompilationOwnershipTests {

	private static readonly Cell Initial = new Cell( 1.0f, 2.0f, 300.0f, 200.0f );
	private static readonly TimeSpan Step = TimeSpan.FromSeconds( 0.1f );

	private ServiceProvider _services;
	private DenseGridSource<Cell> _source;
	private IGridAssembly<Cell> _assembly;
	private ICompiledGridAssembly<Cell> _compiled;
	private Strategy _strategy;

	[SetUp]
	public void SetUp() {
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
		_source = new DenseGridSource<Cell>( 3, 3 );
		for( int row = 0; row < 3; row++ ) {
			for( int column = 0; column < 3; column++ ) {
				Assert.That( _source.TrySetCell( column, row, Initial ), Is.True );
			}
		}
		_assembly = new GridAssembly<Cell>();
		Assert.That( _assembly.TryAttach( _source, 0, 0 ).Succeeded, Is.True );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		_strategy = new Strategy( materials.GetIndex( "hull" ) );
	}

	[TearDown]
	public void TearDown() {
		_compiled.Dispose();
		_services.Dispose();
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Create_CallerCompilation_UsesProvidedOwnerWithoutCompilerService(
		SimulationKind kind
	) {
		using IDisposable simulation = Create( kind, _compiled );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _services.GetService<IGridCompiler>(), Is.Null );
			Assert.That( _compiled.Layers.Count, Is.GreaterThan( 0 ) );
			if( simulation is IAtmosphere atmosphere ) {
				Assert.That( _compiled.Layers, Does.Contain( atmosphere.Temperature ) );
			} else if( simulation is IThermal thermal ) {
				Assert.That( _compiled.Layers, Does.Contain( thermal.Temperature ) );
			} else if( simulation is IAtmosphericThermals combined ) {
				Assert.That( _compiled.Layers, Does.Contain( combined.Atmosphere.Temperature ) );
				Assert.That( _compiled.Layers, Does.Contain( combined.Thermal.Temperature ) );
			}
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Dispose_BorrowedCompilation_RemovesOnlyOwnedLayersAndKeepsOwnerUsable(
		SimulationKind kind
	) {
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		IDisposable simulation = Create( kind, _compiled );

		simulation.Dispose();
		simulation.Dispose();

		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
		Assert.DoesNotThrow( () => _compiled.Commit() );
		Assert.DoesNotThrow( () => _compiled.CreateLayer<float>() );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Dispose_DirtyPhysicalState_DoesNotImplicitlyCommit(
		SimulationKind kind
	) {
		IDisposable simulation = Create( kind, _compiled );
		AddInputs( simulation );

		simulation.Dispose();
		_compiled.Commit();

		Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( Initial ) );
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Dispose_OneBorrower_KeepsOtherBorrowerOperational(
		bool disposeAtmosphere
	) {
		using IAtmosphere atmosphere = _services.GetRequiredService<IGridAtmospherics>().Create( _compiled, _strategy );
		using IThermal thermal = _services.GetRequiredService<IGridThermals>().Create( _compiled, _strategy );

		if( disposeAtmosphere ) {
			atmosphere.Dispose();
			thermal.AddEnergy( 1, 1, 1000.0f );
			_ = thermal.Update( TimeSpan.Zero );
			_compiled.Commit();
			Assert.That( _source.GetCell( 1, 1 ).StructureTemperature, Is.EqualTo( 201.0f ) );
		} else {
			thermal.Dispose();
			atmosphere.AddGas( 1, 1, atmosphere.Gases.GetIndex( "N2" ), 1.0f );
			_ = atmosphere.Update( TimeSpan.Zero );
			_compiled.Commit();
			Assert.That( _source.GetCell( 1, 1 ).Gas, Is.EqualTo( 2.0f ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Dispose_CompilationDisposedFirst_ReportsLifetimeViolation(
		SimulationKind kind
	) {
		IDisposable simulation = Create( kind, _compiled );
		_compiled.Dispose();

		Assert.Throws<ObjectDisposedException>( () => simulation.Dispose() );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Create_NullCompilation_ThrowsArgumentNullException(
		SimulationKind kind
	) {
		Assert.Throws<ArgumentNullException>( () => Create( kind, null ) );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Create_UnsupportedChunkSize_ThrowsWithoutRegisteringLayers(
		SimulationKind kind
	) {
		using ICompiledGridAssembly<Cell> compiled = new GridCompiler().Compile( new GridAssembly<Cell>(), 64 );
		IGridLayer<int> callerLayer = compiled.CreateLayer<int>();

		Assert.Throws<ArgumentException>( () => Create( kind, compiled ) );

		Assert.That( compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Create_StaleCompilation_ThrowsWithoutChangingCallerLayers(
		SimulationKind kind
	) {
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		InvalidateCompilation();

		Assert.Throws<InvalidOperationException>( () => Create( kind, _compiled ) );

		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Update_StaleCompilation_ThrowsWithoutCommittingOrRecompiling(
		SimulationKind kind
	) {
		using IDisposable simulation = Create( kind, _compiled );
		AddInputs( simulation );
		int layerCount = _compiled.Layers.Count;
		InvalidateCompilation();

		InvalidOperationException failure = Assert.Throws<InvalidOperationException>( () => Update( simulation, TimeSpan.Zero ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( failure.Message, Does.Contain( "stale" ) );
			Assert.That( _compiled.Layers.Count, Is.EqualTo( layerCount ) );
			Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( Initial ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Update_TopologyInvalidated_DoesNotImplicitlyCommitPhysicalState(
		SimulationKind kind
	) {
		using IDisposable simulation = Create( kind, _compiled );
		AddInputs( simulation );
		InvalidateTopology( simulation );

		Update( simulation, TimeSpan.Zero );

		Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( Initial ) );
		_compiled.Commit();
		Assert.That( _source.GetCell( 1, 1 ), Is.Not.EqualTo( Initial ) );
	}

	[Test]
	public void Commit_SharedOwner_PersistsGasCondensateAndBothTemperatures() {
		using IAtmosphericThermals simulation = _services.GetRequiredService<IGridAtmosphericThermals>()
			.Create( _compiled, _strategy, _strategy );
		GasIndex gas = simulation.Atmosphere.Gases.GetIndex( "N2" );
		simulation.Atmosphere.AddGas( 1, 1, gas, 3.0f );
		simulation.Atmosphere.AddCondensate( 1, 1, gas, 5.0f );
		_ = simulation.Atmosphere.AddEnergy( 1, 1, 80.0f );
		simulation.Thermal.AddEnergy( 1, 1, 1000.0f );
		Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( Initial ) );

		_compiled.Commit();

		Cell persisted = _source.GetCell( 1, 1 );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( persisted.Gas, Is.EqualTo( 4.0f ) );
			Assert.That( persisted.Condensate, Is.EqualTo( 7.0f ) );
			Assert.That( persisted.GasTemperature, Is.EqualTo( 301.0f ).Within( 0.0001f ) );
			Assert.That( persisted.StructureTemperature, Is.EqualTo( 201.0f ).Within( 0.0001f ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Update_RepeatedTopologyRebuilds_DoesNotAccumulateLayers(
		SimulationKind kind
	) {
		using IDisposable simulation = Create( kind, _compiled );
		int layerCount = _compiled.Layers.Count;

		for( int i = 0; i < 5; i++ ) {
			InvalidateTopology( simulation );
			Update( simulation, TimeSpan.Zero );
			Assert.That( _compiled.Layers.Count, Is.EqualTo( layerCount ) );
		}
	}

	[Test]
	public void Create_AtmosphereTemperatureBindingThrows_RemovesPartiallyCreatedLayers() {
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		Strategy strategy = new Strategy( _strategy.Material, failAtmosphereTemperature: true );

		Assert.Throws<InvalidOperationException>( () => _services.GetRequiredService<IGridAtmospherics>().Create( _compiled, strategy ) );

		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
		Assert.DoesNotThrow( () => _compiled.Commit() );
	}

	[Test]
	public void Create_CombinedThermalBindingThrows_RemovesBothChildrenLayers() {
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		Strategy strategy = new Strategy( _strategy.Material, failMaterial: true );

		Assert.Throws<InvalidOperationException>( () => _services.GetRequiredService<IGridAtmosphericThermals>().Create( _compiled, _strategy, strategy ) );

		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
		Assert.DoesNotThrow( () => _compiled.Commit() );
	}

	[Test]
	public void Create_ConvectionBindingThrows_RemovesTemporaryAndChildLayers() {
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		MaterialReads reads = new MaterialReads { Remaining = 4 * 9 };
		Strategy strategy = new Strategy( _strategy.Material, materialReads: reads );

		Assert.Throws<InvalidOperationException>( () => _services.GetRequiredService<IGridAtmosphericThermals>().Create( _compiled, _strategy, strategy ) );

		Assert.That( reads.Remaining, Is.EqualTo( -1 ) );
		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
		Assert.DoesNotThrow( () => _compiled.Commit() );
	}

	[Test]
	public void Atmosphere_CombinedPublicView_DoesNotExposeIndependentLifecycle() {
		using( Assert.EnterMultipleScope() ) {
			Assert.That( typeof( IAtmosphericThermals ).GetProperty( nameof( IAtmosphericThermals.Atmosphere ) ).PropertyType, Is.EqualTo( typeof( IAtmosphereState ) ) );
			Assert.That( typeof( IAtmosphericThermals ).GetProperty( nameof( IAtmosphericThermals.Thermal ) ).PropertyType, Is.EqualTo( typeof( IThermalState ) ) );
			Assert.That( typeof( IAtmosphereState ).GetMethod( nameof( IAtmosphere.Update ) ), Is.Null );
			Assert.That( typeof( IThermalState ).GetMethod( nameof( IThermal.Update ) ), Is.Null );
			Assert.That( typeof( IAtmosphereState ).GetInterfaces(), Does.Not.Contain( typeof( IDisposable ) ) );
			Assert.That( typeof( IThermalState ).GetInterfaces(), Does.Not.Contain( typeof( IDisposable ) ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Update_UnchangedTopology_DoesNotAllocateAfterWarmup(
		SimulationKind kind
	) {
		using IDisposable simulation = Create( kind, _compiled );
		for( int i = 0; i < 100; i++ ) {
			Update( simulation, Step );
		}

		long before = GC.GetAllocatedBytesForCurrentThread();
		for( int i = 0; i < 100; i++ ) {
			Update( simulation, Step );
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		Assert.That( allocated, Is.Zero );
	}

	private IDisposable Create(
		SimulationKind kind,
		ICompiledGridAssembly<Cell> compiled
	) {
		return kind switch {
			SimulationKind.Atmosphere => _services.GetRequiredService<IGridAtmospherics>().Create( compiled, _strategy ),
			SimulationKind.Thermal => _services.GetRequiredService<IGridThermals>().Create( compiled, _strategy ),
			SimulationKind.Combined => _services.GetRequiredService<IGridAtmosphericThermals>().Create( compiled, _strategy, _strategy ),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
	}

	private void InvalidateCompilation() {
		DenseGridSource<Cell> source = new DenseGridSource<Cell>( 1, 1 );
		Assert.That( source.TrySetCell( 0, 0, Initial ), Is.True );
		Assert.That( _assembly.TryAttach( source, 64, 0 ).Succeeded, Is.True );
		Assert.That( _compiled.IsStale, Is.True );
	}

	private static void AddInputs(
		IDisposable simulation
	) {
		IAtmosphereState atmosphere = simulation is IAtmosphericThermals combined ? combined.Atmosphere : simulation as IAtmosphere;
		if( atmosphere is not null ) {
			atmosphere.AddGas( 1, 1, atmosphere.Gases.GetIndex( "N2" ), 1.0f );
		}
		IThermalState thermal = simulation is IAtmosphericThermals coupled ? coupled.Thermal : simulation as IThermal;
		thermal?.AddEnergy( 1, 1, 1000.0f );
	}

	private static void Update(
		IDisposable simulation,
		TimeSpan elapsed
	) {
		switch( simulation ) {
			case IAtmosphere atmosphere:
				_ = atmosphere.Update( elapsed );
				break;
			case IThermal thermal:
				_ = thermal.Update( elapsed );
				break;
			case IAtmosphericThermals combined:
				_ = combined.Update( elapsed );
				break;
			default:
				throw new ArgumentException( "Unknown simulation.", nameof( simulation ) );
		}
	}

	private static void InvalidateTopology(
		IDisposable simulation
	) {
		switch( simulation ) {
			case IAtmosphere atmosphere:
				atmosphere.InvalidateTopology();
				break;
			case IThermal thermal:
				thermal.InvalidateTopology();
				break;
			case IAtmosphericThermals combined:
				combined.InvalidateTopology();
				break;
			default:
				throw new ArgumentException( "Unknown simulation.", nameof( simulation ) );
		}
	}

	public enum SimulationKind {
		Atmosphere,
		Thermal,
		Combined
	}

	private readonly record struct Cell(
		float Gas,
		float Condensate,
		float GasTemperature,
		float StructureTemperature
	);

	private readonly struct Strategy : IAtmosphereCellStrategy<Cell>, IThermalCellStrategy<Cell> {

		private readonly bool _failAtmosphereTemperature;
		private readonly bool _failMaterial;
		private readonly MaterialReads _materialReads;

		public Strategy(
			MaterialIndex material,
			bool failAtmosphereTemperature = false,
			bool failMaterial = false,
			MaterialReads materialReads = null
		) {
			Material = material;
			_failAtmosphereTemperature = failAtmosphereTemperature;
			_failMaterial = failMaterial;
			_materialReads = materialReads;
		}

		public MaterialIndex Material { get; }

		float IAtmosphereCellStrategy<Cell>.GetGas( in Cell cell, GasIndex gas ) => cell.Gas;
		void IAtmosphereCellStrategy<Cell>.SetGas( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Gas = amount };
		float IAtmosphereCellStrategy<Cell>.GetCondensate( in Cell cell, GasIndex gas ) => cell.Condensate;
		void IAtmosphereCellStrategy<Cell>.SetCondensate( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Condensate = amount };
		float IAtmosphereCellStrategy<Cell>.GetTemperature( in Cell cell ) => _failAtmosphereTemperature ? throw new InvalidOperationException( "Expected temperature binding failure." ) : cell.GasTemperature;
		void IAtmosphereCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { GasTemperature = temperature };
		bool IAtmosphereCellStrategy<Cell>.IsPermeable( in Cell cell ) => true;
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => cell.StructureTemperature;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { StructureTemperature = temperature };

		MaterialIndex IThermalCellStrategy<Cell>.GetMaterial(
			in Cell cell
		) {
			if( _failMaterial || ( _materialReads is not null && --_materialReads.Remaining < 0 ) ) {
				throw new InvalidOperationException( "Expected material binding failure." );
			}
			return Material;
		}
	}

	private sealed class MaterialReads {
		public int Remaining { get; set; }
	}

	private sealed class GasSource : IGasDefinitionSource {
		public IEnumerable<GasDefinition> GetDefinitions() {
			return [new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f )];
		}
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() {
			return [new MaterialDefinition( "hull", "Hull", Conductivity: 0.0f, HeatCapacity: 1000.0f, Emissivity: 0.0f, Absorptivity: 0.0f, Convection: 10.0f )];
		}
	}

	private sealed class Settings : IAtmosphericsSettings, IThermalsSettings, IAtmosphericThermalsSettings {
		public float Acceleration => 2.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.0f;
		public float HeatCapacity => 20.0f;
		public float CondensationRate => 0.05f;
		public int MaxStepsPerUpdate => 1;
		public float WindScale => 1.0f;
		public float StefanBoltzmann => 0.0f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 0.0f;
		public float ExchangeInterval => 0.1f;
	}

}
