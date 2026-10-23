using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.IntegrationTests;

[TestFixture]
internal sealed class TopologyInvalidationTests {

	private static readonly TimeSpan Step = TimeSpan.FromSeconds( 0.1f );
	private const int Origin = -32;
	private ServiceProvider _services;
	private DenseGridSource<Cell> _source;
	private IGridAssembly<Cell> _assembly;
	private ICompiledGridAssembly<Cell> _compiled;
	private PlacementId _placement;
	private MaterialIndex _hull;
	private MaterialIndex _insulator;
	private Settings _settings;
	private Failures _failures;
	private IDisposable _simulation;

	[SetUp]
	public void SetUp() {
		IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasSource()] ) ).Build();
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
		_hull = materials.GetIndex( "hull" );
		_insulator = materials.GetIndex( "insulator" );
		_settings = new Settings();
		_failures = new Failures();
		_simulation = null;
		_services = new ServiceCollection()
			.AddSingleton( gases )
			.AddSingleton( materials )
			.AddSingleton<IAtmosphericsSettings>( _settings )
			.AddSingleton<IThermalsSettings>( _settings )
			.AddSingleton<IAtmosphericThermalsSettings>( _settings )
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		_source = new DenseGridSource<Cell>( 96, 5 );
		for( int row = 1; row <= 3; row++ ) {
			for( int column = 30; column <= 32; column++ ) {
				bool floor = column == 31 && row == 2;
				Assert.That( _source.TrySetCell( column, row, NewCell( floor, floor ? 100.0f : 0.0f ) ), Is.True );
			}
		}
		_assembly = new GridAssembly<Cell>();
		AttachResult attached = _assembly.TryAttach( _source, Origin, 0 );
		Assert.That( attached.Succeeded, Is.True );
		_placement = attached.Placement;
		_compiled = new GridCompiler().Compile( _assembly, 32 );
	}

	[TearDown]
	public void TearDown() {
		_simulation?.Dispose();
		_compiled.Dispose();
		_services.Dispose();
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_CellAddedInsideExistingChunk_PublishesDerivedStateWithoutStepping(
		bool full
	) {
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		long version = _compiled.ChunkLayout.Version;
		Assert.That( _assembly.TryAddCell( _placement, 33, 2, NewCell( true, 25.0f ) ), Is.True );
		Assert.That( _compiled.ChunkLayout.Version, Is.EqualTo( version ) );
		Invalidate( full, new Rect( 1, 2, 1, 1 ) );

		Assert.That( Update( TimeSpan.Zero ), Is.Zero );

		using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
		SimulationFrameLayer pressure = frame.Pressure;
		SimulationFrameLayer gas = frame.GetGas( atmosphere.Gases.GetIndex( "N2" ) );
		using( Assert.EnterMultipleScope() ) {
			Assert.That( _compiled.IsStale, Is.False );
			Assert.That( pressure.TryGet( 1, 2, out float value ), Is.True );
			Assert.That( value, Is.EqualTo( 25.0f * 300.0f / 293.15f ).Within( 0.001f ) );
			Assert.That( gas.TryGet( 1, 2, out float amount ), Is.True );
			Assert.That( amount, Is.EqualTo( 25.0f ) );
		}
	}

	[Test]
	public void InvalidateTopology_NewChunk_GrowsTrackingAndPublishesAllQuantities() {
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		int capacity = _compiled.ChunkLayout.ChunkCapacity;
		Assert.That( _assembly.TryAddCell( _placement, 64, 2, NewCell( true, 25.0f ) ), Is.True );
		Assert.That( _compiled.ChunkLayout.ChunkCapacity, Is.GreaterThan( capacity ) );
		Invalidate( false, new Rect( 32, 2, 1, 1 ) );

		Assert.That( Update( TimeSpan.Zero ), Is.Zero );

		using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
		SimulationFrameLayer pressure = frame.Pressure;
		SimulationFrameLayer wind = frame.WindX;
		Assert.That( pressure.TryGet( 32, 2, out float value ) && value > 0.0f, Is.True );
		Assert.That( wind.TryGet( 32, 2, out float velocity ) && velocity == 0.0f, Is.True );
	}

	[TestCase( int.MinValue, int.MinValue, 0, 0 )]
	[TestCase( int.MaxValue - 95, int.MaxValue - 4, 95, 4 )]
	public void InvalidateTopology_CoordinateLimit_ClampsAffectedRingAndPublishesWithoutStepping(
		int originColumn,
		int originRow,
		int column,
		int row
	) {
		_compiled.Dispose();
		Assert.That( _assembly.TryDetach( _placement ), Is.True );
		Assert.That( _source.TrySetCell( column == 0 ? 1 : 94, row, NewCell( true, 1.0f ) ), Is.True );
		AttachResult attached = _assembly.TryAttach( _source, originColumn, originRow );
		Assert.That( attached.Succeeded, Is.True );
		_placement = attached.Placement;
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		long version = _compiled.ChunkLayout.Version;
		Assert.That( _assembly.TryAddCell( _placement, column, row, NewCell( true, 25.0f ) ), Is.True );
		Assert.That( _compiled.ChunkLayout.Version, Is.EqualTo( version ) );
		int x = originColumn + column;
		int y = originRow + row;
		Invalidate( false, new Rect( x, y, 1, 1 ) );

		Assert.That( Update( TimeSpan.Zero ), Is.Zero );

		using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
		SimulationFrameLayer pressure = frame.Pressure;
		Assert.That( pressure.TryGet( x, y, out float value ) && value > 0.0f, Is.True );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_CellRemoved_PublishesHoleWithoutChangingHeldLease(
		bool full
	) {
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		GasIndex nitrogen = atmosphere.Gases.GetIndex( "N2" );
		AtmosphereFrameLease old = atmosphere.Frames.AcquireLatest();
		long version = _compiled.ChunkLayout.Version;
		Assert.That( _assembly.TryRemoveCell( _placement, 31, 2 ), Is.True );
		Assert.That( _compiled.ChunkLayout.Version, Is.EqualTo( version ) );
		Invalidate( full, new Rect( -1, 2, 1, 1 ) );

		_ = Update( TimeSpan.Zero );

		Assert.That( old.GetGas( nitrogen ).TryGet( -1, 2, out float amount ), Is.True );
		Assert.That( amount, Is.EqualTo( 100.0f ) );
		old.Dispose();
		using AtmosphereFrameLease current = atmosphere.Frames.AcquireLatest();
		Assert.That( current.GetGas( nitrogen ).TryGet( -1, 2, out _ ), Is.False );
	}

	[TestCase( Kind.Atmosphere )]
	[TestCase( Kind.Thermal )]
	[TestCase( Kind.Combined )]
	public void InvalidateTopology_AllCellsRemoved_HandlesRetainedEmptyChunks(
		Kind kind
	) {
		_ = Create( kind );
		for( int row = 1; row <= 3; row++ ) {
			for( int column = 30; column <= 32; column++ ) {
				Assert.That( _assembly.TryRemoveCell( _placement, column, row ), Is.True );
			}
		}
		Invalidate( false, new Rect( -2, 1, 3, 3 ) );

		Assert.DoesNotThrow( () => Update( Step ) );

		Assert.That( _compiled.IsStale, Is.False );
		if( GetAtmosphere() is IAtmosphereState atmosphere ) {
			using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
			Assert.That( frame.Pressure.IsPopulated( -1, 2 ), Is.False );
		}
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_DoorOpenedAcrossChunkBoundary_RefreshesPermeabilityAndConnectivity(
		bool full
	) {
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		_ = Update( Step );
		Assert.That( atmosphere.Vented, Is.Zero );
		long version = _compiled.ChunkLayout.Version;
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true };
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		_ = Update( Step );
		_ = Update( Step );

		Assert.That( _compiled.ChunkLayout.Version, Is.EqualTo( version ) );
		Assert.That( atmosphere.GetTotalGas( 0, 2 ), Is.GreaterThan( 0.0f ) );
		Assert.That( atmosphere.Vented, Is.GreaterThan( 0.0f ) );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_DoorClosed_CancelsBlockedMomentumBeforeZeroStepPublication(
		bool full
	) {
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true };
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		_ = Update( Step );
		Assert.That( atmosphere.WindX[-1, 2], Is.GreaterThan( 0.0f ) );
		_compiled.Commit();
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = false };
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		Assert.That( Update( TimeSpan.Zero ), Is.Zero );

		using AtmosphereFrameLease frame = atmosphere.Frames.AcquireLatest();
		SimulationFrameLayer wind = frame.WindX;
		Assert.That( atmosphere.WindX[-1, 2], Is.Zero );
		Assert.That( atmosphere.WindX[0, 2], Is.Zero );
		Assert.That( wind.TryGet( -1, 2, out float value ) && value == 0.0f, Is.True );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_UnchangedOpenFaces_PreservesMomentum(
		bool full
	) {
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true };
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		_ = Update( Step );
		float wind = atmosphere.WindX[-1, 2];
		float gas = atmosphere.GetTotalGas( -1, 2 );
		Assert.That( wind, Is.GreaterThan( 0.0f ) );
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		_ = Update( TimeSpan.Zero );

		Assert.That( atmosphere.WindX[-1, 2], Is.EqualTo( wind ) );
		Assert.That( atmosphere.GetTotalGas( -1, 2 ), Is.EqualTo( gas ) );
	}

	[Test]
	public void InvalidateTopology_SeamNeighbourBecomesSolid_RefreshesBothPlacements() {
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true };
		DenseGridSource<Cell> neighbour = new DenseGridSource<Cell>( 1, 1 );
		Assert.That( neighbour.TrySetCell( 0, 0, NewCell( true, 25.0f ) ), Is.True );
		_compiled.Dispose();
		Assert.That( _assembly.TryAttach( neighbour, 1, 2 ).Succeeded, Is.True );
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		_ = Update( Step );
		Assert.That( MathF.Abs( atmosphere.WindX[1, 2] ), Is.GreaterThan( 0.0f ) );
		_compiled.Commit();
		neighbour.GetCell( 0, 0 ) = neighbour.GetCell( 0, 0 ) with { Permeable = false };
		Invalidate( false, new Rect( 1, 2, 1, 1 ) );

		_ = Update( TimeSpan.Zero );

		Assert.That( atmosphere.WindX[1, 2], Is.Zero );
		float gas = atmosphere.GetTotalGas( 1, 2 );
		_ = Update( Step );
		Assert.That( atmosphere.GetTotalGas( 1, 2 ), Is.EqualTo( gas ) );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_BreachOpenedAndClosed_RefreshesVacuumFaces(
		bool full
	) {
		IAtmosphere atmosphere = (IAtmosphere)Create( Kind.Atmosphere );
		Assert.That( _assembly.TryRemoveCell( _placement, 32, 2 ), Is.True );
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );
		_ = Update( Step );
		Assert.That( atmosphere.Vented, Is.GreaterThan( 0.0f ) );
		Assert.That( _assembly.TryAddCell( _placement, 32, 2, NewCell( false, 0.0f ) ), Is.True );
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		_ = Update( Step );

		Assert.That( atmosphere.Vented, Is.Zero );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_DiagonalCellRemoved_RefreshesNeighbourVacuumFlags(
		bool full
	) {
		_ = Create( Kind.Atmosphere );
		Assert.That( _assembly.TryRemoveCell( _placement, 32, 1 ), Is.True );
		Invalidate( full, new Rect( 0, 1, 1, 1 ) );

		_ = Update( TimeSpan.Zero );

		Assert.That( _compiled.Layers.OfType<IGridLayer<Direction>>()
			.Any( layer => ( layer[-1, 2] & Direction.NorthEast ) != 0 ), Is.True );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_MaterialChanged_UsesNewCapacityWithoutRebindingTemperature(
		bool full
	) {
		IThermal thermal = (IThermal)Create( Kind.Thermal );
		thermal.AddEnergy( -1, 2, 1000.0f );
		IGridLayer<float> temperature = thermal.Temperature;
		_source.GetCell( 31, 2 ) = _source.GetCell( 31, 2 ) with { Material = _insulator };
		Invalidate( full, new Rect( -1, 2, 1, 1 ) );
		_ = Update( TimeSpan.Zero );

		thermal.AddEnergy( -1, 2, 1000.0f );

		Assert.That( thermal.Temperature, Is.SameAs( temperature ) );
		Assert.That( thermal.Temperature[-1, 2], Is.EqualTo( 201.5f ) );
		Assert.That( _source.GetCell( 31, 2 ).StructureTemperature, Is.EqualTo( 200.0f ), "Invalidation must not commit or re-extract physical state." );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_ConductivityChanged_ConductsAcrossRefreshedFaces(
		bool full
	) {
		IThermal thermal = (IThermal)Create( Kind.Thermal );
		thermal.AddEnergy( -1, 2, 100000.0f );
		MaterialIndex metal = thermal.Materials.GetIndex( "metal" );
		_source.GetCell( 31, 2 ) = _source.GetCell( 31, 2 ) with { Material = metal };
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Material = metal };
		Invalidate( full, new Rect( -1, 2, 2, 1 ) );

		_ = Update( Step );

		Assert.That( thermal.Temperature[-1, 2], Is.LessThan( 300.0f ) );
		Assert.That( thermal.Temperature[0, 2], Is.GreaterThan( 200.0f ) );
		Assert.That( thermal.Temperature[-1, 2] + thermal.Temperature[0, 2], Is.EqualTo( 500.0f ).Within( 0.0001f ) );
	}

	[Test]
	public void InvalidateTopology_EmissivityChanged_RadiatesFromRefreshedMaterial() {
		_settings.StefanBoltzmann = 5.67e-8f;
		IThermal thermal = (IThermal)Create( Kind.Thermal );
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Material = thermal.Materials.GetIndex( "metal" ) };
		Invalidate( false, new Rect( 0, 2, 1, 1 ) );

		_ = Update( Step );

		Assert.That( thermal.Temperature[0, 2], Is.LessThan( 200.0f ) );
		Assert.That( thermal.Temperature[-1, 2], Is.EqualTo( 200.0f ) );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void InvalidateTopology_OccluderRemovedAndReplaced_RefreshesSolarExposure(
		bool full
	) {
		_settings.SolarFlux = 1000.0f;
		IThermal thermal = (IThermal)Create( Kind.Thermal );
		thermal.SunDirection = new Vector( 1.0f, 0.0f );
		_ = Update( Step );
		Assert.That( thermal.Temperature[-1, 2], Is.EqualTo( 200.0f ) );
		Assert.That( _assembly.TryRemoveCell( _placement, 32, 2 ), Is.True );
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );
		_ = Update( Step );
		float lit = thermal.Temperature[-1, 2];
		Assert.That( lit, Is.GreaterThan( 200.0f ) );
		Assert.That( _assembly.TryAddCell( _placement, 32, 2, NewCell( false, 0.0f ) ), Is.True );
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		_ = Update( Step );

		Assert.That( thermal.Temperature[-1, 2], Is.EqualTo( lit ) );
	}

	[Test]
	public void InvalidateTopology_AbsorptivityChanged_RefreshesSolarPowerWithoutOccupancyChange() {
		_settings.SolarFlux = 1000.0f;
		IThermal thermal = (IThermal)Create( Kind.Thermal );
		thermal.SunDirection = new Vector( 1.0f, 0.0f );
		_ = Update( Step );
		float lit = thermal.Temperature[0, 2];
		Assert.That( lit, Is.GreaterThan( 200.0f ) );
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Material = _insulator };
		Invalidate( false, new Rect( 0, 2, 1, 1 ) );

		_ = Update( Step );

		Assert.That( thermal.Temperature[0, 2], Is.EqualTo( lit ) );
	}

	[TestCase( "add" )]
	[TestCase( "remove" )]
	[TestCase( "permeability" )]
	[TestCase( "material" )]
	public void InvalidateTopology_CombinedMetadataChanged_RebuildsLinksAndConservesEnergy(
		string edit
	) {
		_settings.Acceleration = 0.0f;
		if( edit == "add" ) {
			Assert.That( _assembly.TryRemoveCell( _placement, 32, 2 ), Is.True );
		}
		IAtmosphericThermals combined = (IAtmosphericThermals)Create( Kind.Combined );
		switch( edit ) {
			case "add":
				Assert.That( _assembly.TryAddCell( _placement, 32, 2, NewCell( false, 0.0f ) ), Is.True );
				break;
			case "remove":
				Assert.That( _assembly.TryRemoveCell( _placement, 32, 2 ), Is.True );
				break;
			case "permeability":
				_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true };
				break;
			case "material":
				_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Material = _insulator };
				break;
		}
		double energy = GetEnergy( combined );
		Invalidate( false, new Rect( 0, 2, 1, 1 ) );
		Invalidate( true, default );
		Invalidate( false, new Rect( 0, 2, 1, 1 ) );
		int layers = _compiled.Layers.Count;

		Assert.That( Update( Step ), Is.EqualTo( 1 ) );

		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		Assert.That( GetEnergy( combined ), Is.EqualTo( energy ).Within( 0.5 ) );
		Assert.That( combined.Thermal.Temperature[-1, 1], Is.GreaterThan( 200.0f ) );
		if( edit is "permeability" or "material" ) {
			Assert.That( combined.Thermal.Temperature[0, 2], Is.EqualTo( 200.0f ) );
		} else if( edit == "add" ) {
			Assert.That( combined.Thermal.Temperature[0, 2], Is.GreaterThan( 200.0f ) );
		}
	}

	[TestCase( Kind.Atmosphere, false )]
	[TestCase( Kind.Atmosphere, true )]
	[TestCase( Kind.Thermal, false )]
	[TestCase( Kind.Thermal, true )]
	[TestCase( Kind.Combined, false )]
	[TestCase( Kind.Combined, true )]
	public void InvalidateTopology_BindingFails_KeepsRegistrationsAndPendingRetry(
		Kind kind,
		bool full
	) {
		_ = Create( kind );
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true, Material = _insulator };
		if( kind == Kind.Atmosphere ) {
			_failures.PermeabilityReads = 10;
		} else {
			_failures.MaterialReads = 10;
		}
		int layers = _compiled.Layers.Count;
		Invalidate( full, new Rect( 0, 2, 1, 1 ) );

		Assert.Throws<InvalidOperationException>( () => Update( TimeSpan.Zero ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		Assert.Throws<InvalidOperationException>( () => Update( TimeSpan.Zero ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		_failures.PermeabilityReads = int.MaxValue;
		_failures.MaterialReads = int.MaxValue;

		Assert.DoesNotThrow( () => Update( TimeSpan.Zero ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		if( GetThermal() is IThermalState thermal ) {
			thermal.AddEnergy( 0, 2, 1000.0f );
			Assert.That( thermal.Temperature[0, 2], Is.EqualTo( 200.5f ) );
		}
		if( GetAtmosphere() is IAtmosphereState atmosphere ) {
			_ = Update( Step );
			Assert.That( atmosphere.GetTotalGas( 0, 2 ), Is.GreaterThan( 0.0f ) );
		}
	}

	[Test]
	public void InvalidateTopology_ConvectionBindingFails_RetriesLinksWithoutLeakingTemporaryLayers() {
		_settings.Acceleration = 0.0f;
		IAtmosphericThermals combined = (IAtmosphericThermals)Create( Kind.Combined );
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Material = _insulator };
		Invalidate( false, new Rect( 0, 2, 1, 1 ) );
		// Four thermal properties consume 36 reads before convection binds its own properties.
		_failures.MaterialReads = 40;
		int layers = _compiled.Layers.Count;
		double energy = GetEnergy( combined );

		Assert.Throws<InvalidOperationException>( () => Update( TimeSpan.Zero ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		Assert.Throws<InvalidOperationException>( () => Update( TimeSpan.Zero ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
		_failures.MaterialReads = int.MaxValue;
		_ = Update( TimeSpan.Zero );
		_ = Update( Step );

		Assert.That( combined.Thermal.Temperature[0, 2], Is.EqualTo( 200.0f ) );
		Assert.That( combined.Thermal.Temperature[-1, 1], Is.GreaterThan( 200.0f ) );
		Assert.That( GetEnergy( combined ), Is.EqualTo( energy ).Within( 0.5 ) );
		Assert.That( _compiled.Layers, Has.Count.EqualTo( layers ) );
	}

	[TestCase( Kind.Atmosphere )]
	[TestCase( Kind.Thermal )]
	[TestCase( Kind.Combined )]
	public void InvalidateTopology_EmptyArea_DoesNotReextractMetadata(
		Kind kind
	) {
		_ = Create( kind );
		_failures.PermeabilityReads = 0;
		_failures.MaterialReads = 0;
		Invalidate( false, default );

		Assert.DoesNotThrow( () => Update( TimeSpan.Zero ) );
	}

	[TestCase( Kind.Atmosphere )]
	[TestCase( Kind.Thermal )]
	[TestCase( Kind.Combined )]
	public void InvalidateTopology_CallerLayerRegistered_PreservesItsOwnershipAndValues(
		Kind kind
	) {
		_ = Create( kind );
		IGridLayer<int> callerLayer = _compiled.CreateLayer<int>();
		callerLayer[-1, 2] = 7;
		_source.GetCell( 32, 2 ) = _source.GetCell( 32, 2 ) with { Permeable = true, Material = _insulator };
		Invalidate( true, default );

		_ = Update( TimeSpan.Zero );
		_simulation.Dispose();

		Assert.That( _compiled.Layers, Is.EqualTo( new IGridLayer[] { callerLayer } ) );
		Assert.That( callerLayer[-1, 2], Is.EqualTo( 7 ) );
	}

	[TestCase( Kind.Atmosphere )]
	[TestCase( Kind.Thermal )]
	[TestCase( Kind.Combined )]
	public void Update_AfterSuccessfulInvalidation_AllocatesNoManagedBytes(
		Kind kind
	) {
		_ = Create( kind );
		Assert.That( _assembly.TryAddCell( _placement, 64, 2, NewCell( true, 25.0f ) ), Is.True );
		Invalidate( false, new Rect( 32, 2, 1, 1 ) );
		_ = Update( TimeSpan.Zero );
		for( int i = 0; i < 100; i++ ) {
			_ = Update( Step );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int i = 0; i < 100; i++ ) {
			_ = Update( Step );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private Cell NewCell( bool permeable, float gas ) => new Cell( permeable, gas, 300.0f, 200.0f, _hull );

	private IDisposable Create( Kind kind ) {
		Strategy strategy = new Strategy( _failures );
		_simulation = kind switch {
			Kind.Atmosphere => _services.GetRequiredService<IGridAtmospherics>().Create( _compiled, strategy ),
			Kind.Thermal => _services.GetRequiredService<IGridThermals>().Create( _compiled, strategy ),
			Kind.Combined => _services.GetRequiredService<IGridAtmosphericThermals>().Create( _compiled, strategy, strategy ),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
		return _simulation;
	}

	private void Invalidate( bool full, Rect area ) {
		switch( _simulation ) {
			case IAtmosphere atmosphere:
				if( full ) {
					atmosphere.InvalidateTopology();
				} else {
					atmosphere.InvalidateTopology( area );
				}
				break;
			case IThermal thermal:
				if( full ) {
					thermal.InvalidateTopology();
				} else {
					thermal.InvalidateTopology( area );
				}
				break;
			case IAtmosphericThermals combined:
				if( full ) {
					combined.InvalidateTopology();
				} else {
					combined.InvalidateTopology( area );
				}
				break;
			default:
				throw new InvalidOperationException( "Create a simulation first." );
		}
	}

	private int Update( TimeSpan elapsed ) => _simulation switch {
		IAtmosphere atmosphere => atmosphere.Update( elapsed ),
		IThermal thermal => thermal.Update( elapsed ),
		IAtmosphericThermals combined => combined.Update( elapsed ),
		_ => throw new InvalidOperationException( "Create a simulation first." )
	};

	private IAtmosphereState GetAtmosphere() => _simulation switch {
		IAtmosphere atmosphere => atmosphere,
		IAtmosphericThermals combined => combined.Atmosphere,
		_ => null
	};

	private IThermalState GetThermal() => _simulation switch {
		IThermal thermal => thermal,
		IAtmosphericThermals combined => combined.Thermal,
		_ => null
	};

	private double GetEnergy( IAtmosphericThermals combined ) {
		double total = 0.0;
		for( int row = 1; row <= 3; row++ ) {
			for( int column = 30; column <= 32; column++ ) {
				if( !_compiled.ChunkLayout.IsPopulated( column + Origin, row ) ) {
					continue;
				}
				float capacity = _source.GetCell( column, row ).Material == _hull ? 1000.0f : 2000.0f;
				int world = column + Origin;
				total += (double)capacity * combined.Thermal.Temperature[world, row];
				total += (double)combined.Atmosphere.GetTotalGas( world, row ) * _settings.HeatCapacity * combined.Atmosphere.Temperature[world, row];
			}
		}
		return total;
	}

	public enum Kind { Atmosphere, Thermal, Combined }

	private readonly record struct Cell(
		bool Permeable,
		float Gas,
		float GasTemperature,
		float StructureTemperature,
		MaterialIndex Material
	);

	private readonly record struct Strategy( Failures Failures ) : IAtmosphereCellStrategy<Cell>, IThermalCellStrategy<Cell> {
		public bool IsPermeable( in Cell cell ) {
			if( Failures.PermeabilityReads == 0 ) {
				throw new InvalidOperationException( "Expected permeability extraction failure." );
			}
			Failures.PermeabilityReads--;
			return cell.Permeable;
		}
		public float GetGas( in Cell cell, GasIndex gas ) => cell.Gas;
		public void SetGas( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Gas = amount };
		public float GetCondensate( in Cell cell, GasIndex gas ) => 0.0f;
		public void SetCondensate( ref Cell cell, GasIndex gas, float amount ) { }
		public float GetTemperature( in Cell cell ) => cell.GasTemperature;
		public void SetTemperature( ref Cell cell, float temperature ) => cell = cell with { GasTemperature = temperature };
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => cell.StructureTemperature;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { StructureTemperature = temperature };
		public MaterialIndex GetMaterial( in Cell cell ) {
			if( Failures.MaterialReads == 0 ) {
				throw new InvalidOperationException( "Expected material extraction failure." );
			}
			Failures.MaterialReads--;
			return cell.Material;
		}
	}

	private sealed class Failures {
		public int PermeabilityReads { get; set; } = int.MaxValue;
		public int MaterialReads { get; set; } = int.MaxValue;
	}

	private sealed class GasSource : IGasDefinitionSource {
		public IEnumerable<GasDefinition> GetDefinitions() => [new GasDefinition( "N2", "Nitrogen", null, null )];
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() => [
			new MaterialDefinition( "hull", "Hull", Conductivity: 0.0f, HeatCapacity: 1000.0f, Emissivity: 0.0f, Absorptivity: 1.0f, Convection: 10.0f ),
			new MaterialDefinition( "insulator", "Insulator", Conductivity: 0.0f, HeatCapacity: 2000.0f, Emissivity: 0.0f, Absorptivity: 0.0f, Convection: 0.0f ),
			new MaterialDefinition( "metal", "Metal", Conductivity: 100.0f, HeatCapacity: 1000.0f, Emissivity: 1.0f, Absorptivity: 0.0f, Convection: 0.0f )
		];
	}

	private sealed class Settings : IAtmosphericsSettings, IThermalsSettings, IAtmosphericThermalsSettings {
		public float Acceleration { get; set; } = 2.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.0f;
		public float HeatCapacity => 20.0f;
		public float CondensationRate => 0.0f;
		public int MaxStepsPerUpdate => 1;
		public float WindScale => 1.0f;
		public float StefanBoltzmann { get; set; }
		public float SpaceTemperature => 2.7f;
		public float SolarFlux { get; set; }
		public float ExchangeInterval => 0.1f;
	}

}
