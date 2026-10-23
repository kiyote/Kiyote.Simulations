using Kiyote.Geometry;
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
	private PlacementId _placement;
	private Strategy _strategy;
	private Settings _settings;

	[SetUp]
	public void SetUp() {
		IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasSource()] ) ).Build();
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
		_settings = new Settings();
		_services = new ServiceCollection()
			.AddSingleton( gases )
			.AddSingleton( materials )
			.AddSingleton<IAtmosphericsSettings>( _settings )
			.AddSingleton<IThermalsSettings>( _settings )
			.AddSingleton<IAtmosphericThermalsSettings>( _settings )
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
		AttachResult attached = _assembly.TryAttach( _source, 0, 0 );
		Assert.That( attached.Succeeded, Is.True );
		_placement = attached.Placement;
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

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_SourceAttached_PreservesCommittedStateAndLoadsNewCells(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		AddInputs( original );
		_compiled.Commit();
		Cell retained = _source.GetCell( 1, 1 );
		original.Dispose();
		_compiled.Dispose();
		Cell incoming = new Cell( 4.0f, 5.0f, 310.0f, 210.0f );
		_ = AttachSource( incoming, -64, 32 );
		_compiled = new GridCompiler().Compile( _assembly, 32 );

		using IDisposable replacement = Create( kind, _compiled );

		AssertState( replacement, retained, 1, 1 );
		AssertState( replacement, incoming, -64, 32 );
		Assert.That( _compiled.IsStale, Is.False );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_SourceDetached_PersistsDetachedValuesAndRemovesPresence(
		SimulationKind kind
	) {
		_compiled.Dispose();
		(DenseGridSource<Cell> detached, PlacementId placement) = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		using IDisposable original = Create( kind, _compiled );
		AddInputs( original );
		AddInputs( original, 64, 0 );
		_compiled.Commit();
		Cell retained = _source.GetCell( 1, 1 );
		Cell detachedValues = detached.GetCell( 0, 0 );
		Assert.That( detachedValues, Is.Not.EqualTo( Initial ) );
		original.Dispose();
		_compiled.Dispose();
		Assert.That( _assembly.TryDetach( placement ), Is.True );
		_compiled = new GridCompiler().Compile( _assembly, 32 );

		using IDisposable replacement = Create( kind, _compiled );

		AssertState( replacement, retained, 1, 1 );
		Assert.That( _compiled.ChunkLayout.IsPopulated( 64, 0 ), Is.False );
		Assert.That( detached.GetCell( 0, 0 ), Is.EqualTo( detachedValues ) );
		IGridAssembly<Cell> otherAssembly = new GridAssembly<Cell>();
		Assert.That( otherAssembly.TryAttach( detached, 0, 0 ).Succeeded, Is.True );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_AllSourcesDetached_CreatesOperationalEmptySimulation(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		AddInputs( original );
		_compiled.Commit();
		Cell retained = _source.GetCell( 1, 1 );
		original.Dispose();
		_compiled.Dispose();
		Assert.That( _assembly.TryDetach( _placement ), Is.True );
		_compiled = new GridCompiler().Compile( _assembly, 32 );

		using IDisposable replacement = Create( kind, _compiled );
		_ = Update( replacement, Step );

		Assert.That( _compiled.ChunkLayout.ChunkCount, Is.Zero );
		Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( retained ) );
		IAtmosphereState atmosphere = GetAtmosphere( replacement );
		if( atmosphere is not null ) {
			using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
			Assert.That( frame.Pressure.IsPopulated( 1, 1 ), Is.False );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_RetainedLayerReference_RemainsDetachedFromReplacement(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		IGridLayer<float> previous = GetAtmosphere( original )?.Temperature ?? GetThermal( original ).Temperature;
		float value = previous[1, 1];
		_compiled.Commit();
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		using IDisposable replacement = Create( kind, _compiled );
		IAtmosphereState atmosphere = GetAtmosphere( replacement );
		IGridLayer<float> current;
		if( atmosphere is not null ) {
			_ = atmosphere.AddEnergy( 1, 1, 100.0f );
			current = atmosphere.Temperature;
		} else {
			IThermalState thermal = GetThermal( replacement );
			thermal.AddEnergy( 1, 1, 1000.0f );
			current = thermal.Temperature;
		}

		_compiled.Commit();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( current, Is.Not.SameAs( previous ) );
			Assert.That( current.Layout, Is.SameAs( _compiled.ChunkLayout ) );
			Assert.That( previous.Layout, Is.Not.SameAs( current.Layout ) );
			Assert.That( current[1, 1], Is.GreaterThan( value ) );
			Assert.That( previous[1, 1], Is.EqualTo( value ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_FractionalElapsedTime_DoesNotCarryIntoReplacement(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		Assert.That( Update( original, TimeSpan.FromMilliseconds( 50 ) ), Is.Zero );
		_compiled.Commit();
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		using IDisposable replacement = Create( kind, _compiled );

		int first = Update( replacement, TimeSpan.FromMilliseconds( 50 ) );
		int second = Update( replacement, TimeSpan.FromMilliseconds( 50 ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( first, Is.Zero );
			Assert.That( second, Is.EqualTo( 1 ) );
		}
	}

	[Test]
	public void Recreate_PendingHeatExchange_RestartsExchangeCadence() {
		_settings.ExchangeInterval = 0.3f;
		using IDisposable original = Create( SimulationKind.Combined, _compiled );
		Assert.That( Update( original, Step ), Is.Zero );
		_compiled.Commit();
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		using IDisposable replacement = Create( SimulationKind.Combined, _compiled );

		int first = Update( replacement, Step );
		int second = Update( replacement, Step );
		int third = Update( replacement, Step );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( first, Is.Zero );
			Assert.That( second, Is.Zero );
			Assert.That( third, Is.EqualTo( 1 ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_AtmosphereFrames_ResetsTransientStateAndPreservesOldLease(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		AddInputs( original );
		_ = Update( original, Step );
		IAtmosphereState previous = GetAtmosphere( original );
		IAtmosphereFrames previousFrames = previous.Frames;
		GasIndex gas = previous.Gases.GetIndex( "N2" );
		using AtmosphereFrameLease held = previousFrames.AcquireLatest();
		float[] snapshot = Snapshot( held.GetGas( gas ) );
		Assert.That( previousFrames.StepCount, Is.EqualTo( 1 ) );
		Assert.That( previous.Vented, Is.GreaterThan( 0.0f ) );
		Assert.That( MathF.Abs( previous.WindX[0, 1] ), Is.GreaterThan( 0.0f ) );
		_compiled.Commit();
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );

		using IDisposable replacement = Create( kind, _compiled );
		IAtmosphereState current = GetAtmosphere( replacement );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( current.Frames, Is.Not.SameAs( previousFrames ) );
			Assert.That( current.Frames.StepCount, Is.Zero );
			Assert.That( current.Frames.Vented, Is.Zero );
			Assert.That( current.Vented, Is.Zero );
			Assert.That( current.WindX[0, 1], Is.Zero );
			Assert.That( current.WindY[0, 1], Is.Zero );
			Assert.That( current.Pressure[1, 1], Is.EqualTo(
				current.GetTotalGas( 1, 1 ) * current.Temperature[1, 1] / AtmosphereSimd.ReferenceTemperature
			).Within( 0.00001f ) );
		}
		_ = Update( replacement, Step );
		_ = Update( replacement, Step );

		using AtmosphereFrameLease fresh = current.Frames.AcquireLatest();
		using( Assert.EnterMultipleScope() ) {
			Assert.That( current.Frames.StepCount, Is.EqualTo( 2 ) );
			Assert.That( previousFrames.StepCount, Is.EqualTo( 1 ) );
			Assert.That( Snapshot( held.GetGas( gas ) ), Is.EqualTo( snapshot ) );
			Assert.That( held.Pressure.IsPopulated( 64, 0 ), Is.False );
			Assert.That( fresh.Pressure.IsPopulated( 64, 0 ), Is.True );
		}
	}

	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_RuntimeSunDirection_RequiresExplicitReapplication(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		Vector direction = new Vector( 1.0f, -1.0f );
		GetThermal( original ).SunDirection = direction;
		_compiled.Commit();
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		using IDisposable replacement = Create( kind, _compiled );
		IThermalState thermal = GetThermal( replacement );
		Assert.That( thermal.SunDirection, Is.EqualTo( new Vector() ) );

		thermal.SunDirection = direction;
		_ = Update( replacement, TimeSpan.Zero );

		Assert.That( thermal.SunDirection, Is.EqualTo( direction ) );
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Commit_BindingThrows_KeepsSimulationUsableForExplicitRetry(
		SimulationKind kind
	) {
		CommitFailure failure = new CommitFailure();
		Strategy strategy = new Strategy( _strategy.Material, commitFailure: failure );
		using IDisposable simulation = Create( kind, _compiled, strategy );
		AddInputs( simulation );
		IAtmosphereState atmosphere = GetAtmosphere( simulation );
		IGridLayer<float> dirty = atmosphere is not null
			? atmosphere.GetGas( atmosphere.Gases.GetIndex( "N2" ) )
			: GetThermal( simulation ).Temperature;
		Assert.That( _compiled.ChunkLayout.TryLocate( 1, 1, out int chunk, out _ ), Is.True );
		int layerCount = _compiled.Layers.Count;

		Assert.Throws<InvalidOperationException>( () => _compiled.Commit() );

		Assert.That( dirty.IsDirty( chunk ), Is.True );
		Assert.That( _compiled.IsStale, Is.False );
		Assert.That( _compiled.Layers.Count, Is.EqualTo( layerCount ) );
		Assert.DoesNotThrow( () => Update( simulation, TimeSpan.Zero ) );
		failure.Enabled = false;
		_compiled.Commit();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( dirty.IsDirty( chunk ), Is.False );
			Assert.That( _source.GetCell( 1, 1 ).Gas, Is.EqualTo( kind == SimulationKind.Thermal ? Initial.Gas : Initial.Gas + 1.0f ) );
			Assert.That( _source.GetCell( 1, 1 ).StructureTemperature, Is.EqualTo( kind == SimulationKind.Atmosphere ? Initial.StructureTemperature : Initial.StructureTemperature + 1.0f ) );
		}
	}

	[TestCase( SimulationKind.Atmosphere )]
	[TestCase( SimulationKind.Thermal )]
	[TestCase( SimulationKind.Combined )]
	public void Recreate_ReplacementBindingThrows_KeepsEditedAssemblyForExplicitRetry(
		SimulationKind kind
	) {
		using IDisposable original = Create( kind, _compiled );
		AddInputs( original );
		_compiled.Commit();
		Cell retained = _source.GetCell( 1, 1 );
		original.Dispose();
		_compiled.Dispose();
		_ = AttachSource( Initial );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		Strategy failing = new Strategy(
			_strategy.Material,
			failAtmosphereTemperature: kind == SimulationKind.Atmosphere,
			failMaterial: kind != SimulationKind.Atmosphere
		);

		Assert.Throws<InvalidOperationException>( () => Create( kind, _compiled, failing ) );

		Assert.That( _compiled.Layers, Is.Empty );
		Assert.That( _compiled.IsStale, Is.False );
		Assert.That( _compiled.ChunkLayout.IsPopulated( 64, 0 ), Is.True );
		Assert.That( _source.GetCell( 1, 1 ), Is.EqualTo( retained ) );
		using IDisposable replacement = Create( kind, _compiled );
		AssertState( replacement, retained, 1, 1 );
		AssertState( replacement, Initial, 64, 0 );
		Assert.DoesNotThrow( () => Update( replacement, TimeSpan.Zero ) );
	}

	private IDisposable Create(
		SimulationKind kind,
		ICompiledGridAssembly<Cell> compiled,
		Strategy? strategy = null
	) {
		Strategy selected = strategy ?? _strategy;
		return kind switch {
			SimulationKind.Atmosphere => _services.GetRequiredService<IGridAtmospherics>().Create( compiled, selected ),
			SimulationKind.Thermal => _services.GetRequiredService<IGridThermals>().Create( compiled, selected ),
			SimulationKind.Combined => _services.GetRequiredService<IGridAtmosphericThermals>().Create( compiled, selected, selected ),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
	}

	private (DenseGridSource<Cell> Source, PlacementId Placement) AttachSource(
		Cell cell,
		int column = 64,
		int row = 0
	) {
		DenseGridSource<Cell> source = new DenseGridSource<Cell>( 1, 1 );
		Assert.That( source.TrySetCell( 0, 0, cell ), Is.True );
		AttachResult attached = _assembly.TryAttach( source, column, row );
		Assert.That( attached.Succeeded, Is.True );
		return (source, attached.Placement);
	}

	private static IAtmosphereState GetAtmosphere(
		IDisposable simulation
	) {
		return simulation is IAtmosphericThermals combined ? combined.Atmosphere : simulation as IAtmosphere;
	}

	private static IThermalState GetThermal(
		IDisposable simulation
	) {
		return simulation is IAtmosphericThermals combined ? combined.Thermal : simulation as IThermal;
	}

	private static void AssertState(
		IDisposable simulation,
		Cell expected,
		int column,
		int row
	) {
		IAtmosphereState atmosphere = GetAtmosphere( simulation );
		IThermalState thermal = GetThermal( simulation );
		using( Assert.EnterMultipleScope() ) {
			if( atmosphere is not null ) {
				GasIndex gas = atmosphere.Gases.GetIndex( "N2" );
				Assert.That( atmosphere.GetGas( gas )[column, row], Is.EqualTo( expected.Gas ) );
				Assert.That( atmosphere.GetCondensate( gas )[column, row], Is.EqualTo( expected.Condensate ) );
				Assert.That( atmosphere.Temperature[column, row], Is.EqualTo( expected.GasTemperature ) );
			}
			if( thermal is not null ) {
				Assert.That( thermal.Temperature[column, row], Is.EqualTo( expected.StructureTemperature ) );
			}
		}
	}

	private static float[] Snapshot(
		SimulationFrameLayer layer
	) {
		List<float> values = [];
		foreach( int chunk in layer.EnumerateChunks() ) {
			values.AddRange( layer.GetValues( chunk ) );
		}
		return [.. values];
	}

	private void InvalidateCompilation() {
		DenseGridSource<Cell> source = new DenseGridSource<Cell>( 1, 1 );
		Assert.That( source.TrySetCell( 0, 0, Initial ), Is.True );
		Assert.That( _assembly.TryAttach( source, 64, 0 ).Succeeded, Is.True );
		Assert.That( _compiled.IsStale, Is.True );
	}

	private static void AddInputs(
		IDisposable simulation,
		int column = 1,
		int row = 1
	) {
		IAtmosphereState atmosphere = GetAtmosphere( simulation );
		if( atmosphere is not null ) {
			atmosphere.AddGas( column, row, atmosphere.Gases.GetIndex( "N2" ), 1.0f );
		}
		GetThermal( simulation )?.AddEnergy( column, row, 1000.0f );
	}

	private static int Update(
		IDisposable simulation,
		TimeSpan elapsed
	) {
		switch( simulation ) {
			case IAtmosphere atmosphere:
				return atmosphere.Update( elapsed );
			case IThermal thermal:
				return thermal.Update( elapsed );
			case IAtmosphericThermals combined:
				return combined.Update( elapsed );
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
		private readonly CommitFailure _commitFailure;

		public Strategy(
			MaterialIndex material,
			bool failAtmosphereTemperature = false,
			bool failMaterial = false,
			MaterialReads materialReads = null,
			CommitFailure commitFailure = null
		) {
			Material = material;
			_failAtmosphereTemperature = failAtmosphereTemperature;
			_failMaterial = failMaterial;
			_materialReads = materialReads;
			_commitFailure = commitFailure;
		}

		public MaterialIndex Material { get; }

		float IAtmosphereCellStrategy<Cell>.GetGas( in Cell cell, GasIndex gas ) => cell.Gas;
		void IAtmosphereCellStrategy<Cell>.SetGas( ref Cell cell, GasIndex gas, float amount ) {
			_commitFailure?.ThrowIfEnabled();
			cell = cell with { Gas = amount };
		}
		float IAtmosphereCellStrategy<Cell>.GetCondensate( in Cell cell, GasIndex gas ) => cell.Condensate;
		void IAtmosphereCellStrategy<Cell>.SetCondensate( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Condensate = amount };
		float IAtmosphereCellStrategy<Cell>.GetTemperature( in Cell cell ) => _failAtmosphereTemperature ? throw new InvalidOperationException( "Expected temperature binding failure." ) : cell.GasTemperature;
		void IAtmosphereCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { GasTemperature = temperature };
		bool IAtmosphereCellStrategy<Cell>.IsPermeable( in Cell cell ) => true;
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => cell.StructureTemperature;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) {
			_commitFailure?.ThrowIfEnabled();
			cell = cell with { StructureTemperature = temperature };
		}

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

	private sealed class CommitFailure {
		public bool Enabled { get; set; } = true;

		public void ThrowIfEnabled() {
			if( Enabled ) {
				throw new InvalidOperationException( "Expected commit failure." );
			}
		}
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
		public float ExchangeInterval { get; set; } = 0.1f;
	}

}
