using Kiyote.Buffers.ChunkedGrids;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals.IntegrationTests;

[TestFixture]
internal sealed class CoupledSteppingTests {

	private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds( 100 );
	private static readonly char[] PublicationOnly = ['P'];
	private static readonly int[] EveryOtherTick = [2, 4];
	private readonly List<Harness> _harnesses = [];

	[TearDown]
	public void TearDown() {
		foreach( Harness harness in _harnesses ) {
			harness.Dispose();
		}
		_harnesses.Clear();
	}

	[Test]
	public void Update_SeveralTicks_StepsBothChildrenThenExchangesAndPublishesOnce() {
		Harness harness = Create();

		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) ), Is.EqualTo( 3 ) );

		Assert.That( harness.Trace.Events, Is.EqualTo( "ATEATEATEP".ToCharArray() ) );
		Assert.That( harness.Trace.PublishedStepsDuringExchange, Is.EqualTo( new long[] { 0, 0, 0 } ) );
		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 3 ) );
		AssertFrames( harness.Simulation.Atmosphere );
	}

	[TestCase( 100, 30, 10 )]
	[TestCase( 125, 25, 15 )]
	public void Update_FractionalBatching_ProducesTheSameTicksAndPhysicalState(
		int stepMilliseconds,
		int fragmentMilliseconds,
		int fragments
	) {
		Harness batched = Create( step: stepMilliseconds / 1000.0f );
		Harness fragmented = Create( step: stepMilliseconds / 1000.0f );
		int expected = fragmentMilliseconds * fragments / stepMilliseconds;
		int exchanges = 0;
		for( int i = 0; i < fragments; i++ ) {
			exchanges += fragmented.Simulation.Update( TimeSpan.FromMilliseconds( fragmentMilliseconds ) );
		}

		Assert.That( batched.Simulation.Update( TimeSpan.FromMilliseconds( fragmentMilliseconds * fragments ) ), Is.EqualTo( expected ) );

		Assert.That( exchanges, Is.EqualTo( expected ) );
		Assert.That( fragmented.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( expected ) );
		AssertState( batched.Simulation, fragmented.Simulation );
	}

	[TestCase( 2, 5 )]
	[TestCase( 5, 2 )]
	public void Update_DifferentChildCaps_AdvancesBothByTheSmallerCap(
		int atmosphereCap,
		int thermalCap
	) {
		Harness harness = Create( atmosphereCap, thermalCap );

		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 500 ) ), Is.EqualTo( 2 ) );

		Assert.That( harness.Trace.Events, Is.EqualTo( "ATEATEP".ToCharArray() ) );
		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 2 ) );
	}

	[Test]
	public void Update_ExactlyAtCap_KeepsTheFractionalRemainder() {
		Harness harness = Create( 2, 5 );

		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 250 ) ), Is.EqualTo( 2 ) );
		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 50 ) ), Is.EqualTo( 1 ) );

		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 3 ) );
	}

	[Test]
	public void Update_ExceedsCap_DropsExcessTicksAndFractionOnce() {
		Harness harness = Create( 2, 5 );

		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 350 ) ), Is.EqualTo( 2 ) );
		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 50 ) ), Is.Zero );
		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 50 ) ), Is.EqualTo( 1 ) );

		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 3 ) );
		Assert.That( harness.Trace.Events.Count( phase => phase == 'T' ), Is.EqualTo( 3 ) );
	}

	[Test]
	public void Update_MaximumElapsed_CapsBeforeNarrowingTheStepCount() {
		Harness harness = Create( 2, 5 );

		Assert.That( harness.Simulation.Update( TimeSpan.MaxValue ), Is.EqualTo( 2 ) );
		Assert.That( harness.Simulation.Update( TimeSpan.Zero ), Is.Zero );

		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 2 ) );
	}

	[TestCase( 0.0f, 1 )]
	[TestCase( 0.04f, 1 )]
	[TestCase( 0.16f, 2 )]
	[TestCase( 0.25f, 2 )]
	[TestCase( 0.35f, 4 )]
	[TestCase( 0.5f, 5 )]
	public void Update_ExchangeInterval_UsesNearestEvenFixedTicksWithAMinimumOfOne(
		float interval,
		int ticks
	) {
		Harness harness = Create( interval: interval );

		Assert.That( harness.Simulation.Update( TimeSpan.FromTicks( Tick.Ticks * ( ticks - 1 ) ) ), Is.Zero );
		Assert.That( harness.Simulation.Update( Tick ), Is.EqualTo( 1 ) );

		Assert.That( harness.Trace.PublishedStepsDuringExchange, Has.Count.EqualTo( 1 ) );
		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( ticks ) );
	}

	[Test]
	public void Update_ExchangeEveryOtherTick_ExchangesAfterTheCorrespondingStructureStep() {
		Harness harness = Create( interval: 0.2f );

		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 500 ) ), Is.EqualTo( 2 ) );

		Assert.That( harness.Trace.StructureStepsDuringExchange, Is.EqualTo( EveryOtherTick ) );
		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 5 ) );
	}

	[Test]
	public void Update_SmallestRepresentableStep_AdvancesExactlyOneTick() {
		Harness harness = Create( step: 0.0000001f, interval: 0.0000001f );

		Assert.That( harness.Simulation.Update( TimeSpan.FromTicks( 1 ) ), Is.EqualTo( 1 ) );

		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.EqualTo( 1 ) );
	}

	[Test]
	public void Update_NoElapsedTime_FinalizesQueuedInputsWithoutStepping() {
		Harness harness = Create();
		IAtmosphereState atmosphere = harness.Simulation.Atmosphere;
		GasIndex nitrogen = atmosphere.Gases.GetIndex( "N2" );
		atmosphere.AddGas( 1, 1, nitrogen, 5.0f, 600.0f );
		atmosphere.AddCondensate( 1, 1, nitrogen, 3.0f );
		_ = atmosphere.AddEnergy( 1, 1, 4200.0f );
		harness.Simulation.Thermal.AddEnergy( 1, 1, 1000.0f );

		Assert.That( harness.Simulation.Update( TimeSpan.Zero ), Is.Zero );

		Assert.That( harness.Trace.Events, Is.EqualTo( PublicationOnly ) );
		Assert.That( atmosphere.Frames.StepCount, Is.Zero );
		Assert.That( atmosphere.GetTotalGas( 1, 1 ), Is.EqualTo( 105.0f ) );
		Assert.That( harness.Simulation.Thermal.Temperature[1, 1], Is.EqualTo( 201.0f ) );
		AssertFrames( atmosphere );
	}

	[Test]
	public void Update_FinalConvection_PublishesCurrentTemperatureAndPressure() {
		Harness harness = Create();
		_ = harness.Simulation.Update( Tick );
		harness.Trace.Clear();

		_ = harness.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) );

		Assert.That( harness.Trace.PublishedStepsDuringExchange, Is.EqualTo( new long[] { 1, 1, 1 } ) );
		Assert.That( harness.Simulation.Atmosphere.Temperature[1, 1], Is.LessThan( 300.0f ) );
		AssertFrames( harness.Simulation.Atmosphere );
	}

	[Test]
	public void Update_ReaderHoldsOldTemperatureLease_NeverChangesTheLeasedBuffer() {
		Harness harness = Create();
		using TripleBufferedLease<float> old = harness.Simulation.Atmosphere.Frames.AcquireTemperature();

		_ = harness.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) );
		_ = harness.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) );

		Assert.That( old.TryGet( 1, 1, out float temperature ), Is.True );
		Assert.That( temperature, Is.EqualTo( 300.0f ) );
		Assert.That( harness.Simulation.Atmosphere.Temperature[1, 1], Is.LessThan( temperature ) );
	}

	[Test]
	public void Update_MultipleVentingTicks_AccumulatesDiagnosticsUntilFinalPublication() {
		Harness batched = Create( initialize: false );
		Harness separate = Create( initialize: false );
		Assert.That( batched.Owner.Assembly.TryRemoveCell( batched.Placement, 2, 1 ), Is.True );
		Assert.That( separate.Owner.Assembly.TryRemoveCell( separate.Placement, 2, 1 ), Is.True );
		batched.Initialize();
		separate.Initialize();
		float vented = 0.0f;
		for( int i = 0; i < 3; i++ ) {
			_ = separate.Simulation.Update( Tick );
			vented += separate.Simulation.Atmosphere.Vented;
		}

		_ = batched.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) );

		Assert.That( vented, Is.GreaterThan( 0.0f ) );
		Assert.That( batched.Simulation.Atmosphere.Vented, Is.EqualTo( vented ).Within( 0.0001f ) );
		Assert.That( batched.Simulation.Atmosphere.Frames.Vented, Is.EqualTo( batched.Simulation.Atmosphere.Vented ) );
		AssertState( batched.Simulation, separate.Simulation );
		_ = batched.Simulation.Update( TimeSpan.Zero );
		Assert.That( batched.Simulation.Atmosphere.Vented, Is.Zero );
		Assert.That( batched.Simulation.Atmosphere.Frames.Vented, Is.Zero );
	}

	[TestCase( 0 )]
	[TestCase( 36 )]
	public void Update_PreparationFails_DoesNotStepPublishOrConsumeElapsedTime(
		int materialReads
	) {
		Harness harness = Create();
		harness.MaterialReads = materialReads;
		harness.Simulation.InvalidateTopology();

		Assert.Throws<InvalidOperationException>( () => harness.Simulation.Update( TimeSpan.FromMilliseconds( 250 ) ) );

		Assert.That( harness.Trace.Events, Is.Empty );
		Assert.That( harness.Simulation.Atmosphere.Frames.StepCount, Is.Zero );
		Assert.That( harness.Simulation.Atmosphere.Temperature[1, 1], Is.EqualTo( 300.0f ) );
		harness.MaterialReads = int.MaxValue;
		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 50 ) ), Is.Zero );
		Assert.That( harness.Simulation.Update( TimeSpan.FromMilliseconds( 50 ) ), Is.EqualTo( 1 ) );
	}

	[Test]
	public void Commit_AfterCoupledBatch_PersistsFinalGasCondensateAndBothTemperatures() {
		Harness harness = Create();
		IAtmosphereState atmosphere = harness.Simulation.Atmosphere;
		GasIndex nitrogen = atmosphere.Gases.GetIndex( "N2" );
		atmosphere.AddGas( 1, 1, nitrogen, 5.0f );
		atmosphere.AddCondensate( 1, 1, nitrogen, 3.0f );
		_ = harness.Simulation.Update( TimeSpan.FromMilliseconds( 300 ) );
		Assert.That( harness.Source.GetCell( 1, 1 ).GasTemperature, Is.EqualTo( 300.0f ) );

		harness.Owner.Commit();

		Cell cell = harness.Source.GetCell( 1, 1 );
		Assert.That( cell.Gas, Is.EqualTo( atmosphere.GetTotalGas( 1, 1 ) ) );
		Assert.That( cell.Condensate, Is.EqualTo( atmosphere.GetCondensate( nitrogen )[1, 1] ) );
		Assert.That( cell.GasTemperature, Is.EqualTo( atmosphere.Temperature[1, 1] ) );
		Assert.That( cell.StructureTemperature, Is.EqualTo( harness.Simulation.Thermal.Temperature[1, 1] ) );
	}

	[Test]
	public void Update_NegativeElapsed_ThrowsWithoutConsumingTime() {
		Harness harness = Create();

		Assert.Throws<ArgumentOutOfRangeException>( () => harness.Simulation.Update( TimeSpan.FromTicks( -1 ) ) );

		Assert.That( harness.Trace.Events, Is.Empty );
		Assert.That( harness.Simulation.Update( Tick ), Is.EqualTo( 1 ) );
	}

	[TestCase( 0.0f )]
	[TestCase( -0.1f )]
	[TestCase( float.NaN )]
	[TestCase( float.PositiveInfinity )]
	[TestCase( float.NegativeInfinity )]
	[TestCase( float.Epsilon )]
	[TestCase( float.MaxValue )]
	public void Create_InvalidClock_ThrowsWithoutLeavingOwnedLayers(
		float step
	) {
		Harness harness = Create( step: step, initialize: false );

		Assert.Throws<ArgumentOutOfRangeException>( harness.Initialize );

		Assert.That( harness.Owner.Layers, Is.Empty );
	}

	[TestCase( 0, 1 )]
	[TestCase( 1, 0 )]
	[TestCase( -1, 2 )]
	[TestCase( 2, -1 )]
	public void Create_NonpositiveChildCap_ThrowsAndCleansBothChildren(
		int atmosphereCap,
		int thermalCap
	) {
		Harness harness = Create( atmosphereCap, thermalCap, initialize: false );

		Assert.Throws<ArgumentOutOfRangeException>( harness.Initialize );

		Assert.That( harness.Owner.Layers, Is.Empty );
	}

	[TestCase( float.NaN )]
	[TestCase( float.PositiveInfinity )]
	[TestCase( float.NegativeInfinity )]
	[TestCase( float.MaxValue )]
	public void Create_UnrepresentableExchangeCadence_ThrowsWithoutLeavingLayers(
		float interval
	) {
		Harness harness = Create( interval: interval, initialize: false );

		Assert.Throws<ArgumentOutOfRangeException>( harness.Initialize );

		Assert.That( harness.Owner.Layers, Is.Empty );
	}

	[Test]
	public void Update_ClockChangesAfterCreation_UsesTheCapturedSharedStep() {
		Harness harness = Create();
		harness.Clock.FixedTimeStep = 0.2f;

		Assert.That( harness.Simulation.Update( Tick ), Is.EqualTo( 1 ) );

		Assert.That( harness.Trace.Events, Is.EqualTo( "ATEP".ToCharArray() ) );
	}

	[Test]
	public void Update_StandaloneChildren_KeepTheirIndependentCaps() {
		Harness harness = Create( 2, 5, initialize: false );
		using IAtmosphere atmosphere = harness.Services.GetRequiredService<IGridAtmospherics>().Create( harness.Owner, new Strategy( harness ) );
		using IThermal thermal = harness.Services.GetRequiredService<IGridThermals>().Create( harness.Owner, new Strategy( harness ) );

		Assert.That( atmosphere.Update( TimeSpan.FromMilliseconds( 500 ) ), Is.EqualTo( 2 ) );
		Assert.That( thermal.Update( TimeSpan.FromMilliseconds( 500 ) ), Is.EqualTo( 5 ) );
	}

	[Test]
	public void Update_CoupledBatchAfterWarmup_AllocatesNoManagedBytes() {
		Harness harness = Create();
		TimeSpan elapsed = TimeSpan.FromMilliseconds( 300 );
		for( int i = 0; i < 100; i++ ) {
			_ = harness.Simulation.Update( elapsed );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int i = 0; i < 100; i++ ) {
			_ = harness.Simulation.Update( elapsed );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private Harness Create( int atmosphereCap = 8, int thermalCap = 8, float step = 0.1f, float interval = 0.1f, bool initialize = true ) {
		Harness harness = new Harness( atmosphereCap, thermalCap, step, interval );
		_harnesses.Add( harness );
		if( initialize ) {
			harness.Initialize();
		}
		return harness;
	}

	private static void AssertFrames( IAtmosphereState atmosphere ) {
		GasIndex nitrogen = atmosphere.Gases.GetIndex( "N2" );
		using TripleBufferedLease<float> pressure = atmosphere.Frames.AcquirePressure();
		using TripleBufferedLease<float> temperature = atmosphere.Frames.AcquireTemperature();
		using TripleBufferedLease<float> gas = atmosphere.Frames.AcquireGas( nitrogen );
		using TripleBufferedLease<float> condensate = atmosphere.Frames.AcquireCondensate( nitrogen );
		using TripleBufferedLease<float> windX = atmosphere.Frames.AcquireWindX();
		using TripleBufferedLease<float> windY = atmosphere.Frames.AcquireWindY();
		Assert.That( pressure.TryGet( 1, 1, out float p ), Is.True );
		Assert.That( temperature.TryGet( 1, 1, out float t ), Is.True );
		Assert.That( gas.TryGet( 1, 1, out float g ), Is.True );
		Assert.That( condensate.TryGet( 1, 1, out float c ), Is.True );
		Assert.That( windX.TryGet( 1, 1, out float x ), Is.True );
		Assert.That( windY.TryGet( 1, 1, out float y ), Is.True );
		Assert.That( t, Is.EqualTo( atmosphere.Temperature[1, 1] ) );
		Assert.That( p, Is.EqualTo( atmosphere.GetTotalGas( 1, 1 ) * t / 293.15f ).Within( 0.0001f ) );
		Assert.That( p, Is.EqualTo( atmosphere.Pressure[1, 1] ) );
		Assert.That( g, Is.EqualTo( atmosphere.GetTotalGas( 1, 1 ) ) );
		Assert.That( c, Is.EqualTo( atmosphere.GetCondensate( nitrogen )[1, 1] ) );
		Assert.That( x, Is.EqualTo( atmosphere.WindX[1, 1] ) );
		Assert.That( y, Is.EqualTo( atmosphere.WindY[1, 1] ) );
	}

	private static void AssertState( IAtmosphericThermals actual, IAtmosphericThermals expected ) {
		for( int row = 0; row < 3; row++ ) {
			for( int column = 0; column < 3; column++ ) {
				bool populated = expected.Atmosphere.Temperature.Layout.IsPopulated( column, row );
				Assert.That( actual.Atmosphere.Temperature.Layout.IsPopulated( column, row ), Is.EqualTo( populated ) );
				if( !populated ) {
					continue;
				}
				Assert.That( actual.Atmosphere.GetTotalGas( column, row ), Is.EqualTo( expected.Atmosphere.GetTotalGas( column, row ) ) );
				Assert.That( actual.Atmosphere.Temperature[column, row], Is.EqualTo( expected.Atmosphere.Temperature[column, row] ) );
				Assert.That( actual.Thermal.Temperature[column, row], Is.EqualTo( expected.Thermal.Temperature[column, row] ) );
				Assert.That( actual.Atmosphere.Pressure[column, row], Is.EqualTo( expected.Atmosphere.Pressure[column, row] ) );
				Assert.That( actual.Atmosphere.WindX[column, row], Is.EqualTo( expected.Atmosphere.WindX[column, row] ) );
				Assert.That( actual.Atmosphere.WindY[column, row], Is.EqualTo( expected.Atmosphere.WindY[column, row] ) );
			}
		}
	}

	private sealed class Harness : IDisposable {
		public ServiceProvider Services { get; }
		public DenseGridSource<Cell> Source { get; }
		public ICompiledGridAssembly<Cell> Owner { get; }
		public PlacementId Placement { get; }
		public IAtmosphericThermals Simulation { get; private set; }
		public PhaseTrace Trace { get; } = new PhaseTrace();
		public TestClock Clock { get; }
		public MaterialIndex Material { get; }
		public int MaterialReads { get; set; } = int.MaxValue;

		public Harness( int atmosphereCap, int thermalCap, float step, float interval ) {
			IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasSource()] ) ).Build();
			IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
			Material = materials.GetIndex( "hull" );
			Settings settings = new Settings( atmosphereCap, thermalCap, interval );
			Clock = new TestClock { FixedTimeStep = step };
			Services = new ServiceCollection()
				.AddSingleton( gases )
				.AddSingleton( materials )
				.AddSingleton<IAtmosphericsSettings>( settings )
				.AddSingleton<IThermalsSettings>( settings )
				.AddSingleton<IAtmosphericThermalsSettings>( settings )
				.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
				.AddLowFidelitySimulations()
				.AddSingleton<ISimulationClock>( Clock )
				.AddSingleton<IAtmospherePressure>( Trace )
				.AddSingleton<IThermalConduction>( Trace )
				.AddSingleton<IAtmosphericThermalsConvection>( Trace )
				.AddSingleton<IAtmosphereWind>( Trace )
				.BuildServiceProvider();
			Source = new DenseGridSource<Cell>( 3, 3 );
			for( int row = 0; row < 3; row++ ) {
				for( int column = 0; column < 3; column++ ) {
					bool floor = column == 1 && row == 1;
					Assert.That( Source.TrySetCell( column, row, new Cell( floor, floor ? 100.0f : 0.0f, floor ? 2.0f : 0.0f, 300.0f, 200.0f ) ), Is.True );
				}
			}
			IGridAssembly<Cell> assembly = new GridAssembly<Cell>();
			AttachResult attached = assembly.TryAttach( Source, 0, 0 );
			Assert.That( attached.Succeeded, Is.True );
			Placement = attached.Placement;
			Owner = new GridCompiler().Compile( assembly, 32 );
		}

		public void Initialize() {
			Simulation = Services.GetRequiredService<IGridAtmosphericThermals>().Create( Owner, new Strategy( this ), new Strategy( this ) );
			Trace.Clear();
		}

		public void Dispose() {
			Simulation?.Dispose();
			Owner.Dispose();
			Services.Dispose();
		}
	}

	private sealed class PhaseTrace : IAtmospherePressure, IThermalConduction, IAtmosphericThermalsConvection, IAtmosphereWind {
		private readonly IAtmospherePressure _pressure = new AtmospherePressure();
		private readonly IThermalConduction _conduction = new ThermalConduction();
		private readonly IAtmosphericThermalsConvection _convection = new AtmosphericThermalsConvection();
		private readonly IAtmosphereWind _wind = new AtmosphereWind();
		public List<char> Events { get; } = new List<char>( 4096 );
		public List<long> PublishedStepsDuringExchange { get; } = new List<long>( 4096 );
		public List<int> StructureStepsDuringExchange { get; } = new List<int>( 4096 );
		private int _structureSteps;

		public void Clear() {
			Events.Clear();
			PublishedStepsDuringExchange.Clear();
			StructureStepsDuringExchange.Clear();
			_structureSteps = 0;
		}
		public void Accumulate( ReadOnlySpan<ulong> presence, ReadOnlySpan<float> gas, Span<float> total ) => _pressure.Accumulate( presence, gas, total );
		public void Compute( ReadOnlySpan<ulong> presence, ReadOnlySpan<float> total, ReadOnlySpan<float> temperature, Span<float> pressure ) {
			Events.Add( 'A' );
			_pressure.Compute( presence, total, temperature, pressure );
		}
		public void Conduct( ReadOnlySpan<ulong> presence, float dt, ReadOnlySpan<float> temperature, ReadOnlySpan<float> conductivity, ReadOnlySpan<float> capacity, Span<float> temperatureNext ) {
			Events.Add( 'T' );
			_structureSteps++;
			_conduction.Conduct( presence, dt, temperature, conductivity, capacity, temperatureNext );
		}
		public void Exchange( ReadOnlySpan<ConvectionCell> cells, ReadOnlySpan<ConvectionLink> links, float dt, float gasHeatCapacity, IAtmosphereState atmosphere, IThermalState thermal ) {
			Events.Add( 'E' );
			PublishedStepsDuringExchange.Add( atmosphere.Frames.StepCount );
			StructureStepsDuringExchange.Add( _structureSteps );
			_convection.Exchange( cells, links, dt, gasHeatCapacity, atmosphere, thermal );
		}
		public void Publish( ReadOnlySpan<ulong> presence, float windScale, ReadOnlySpan<float> temperature, ReadOnlySpan<float> flowEast, ReadOnlySpan<float> flowSouth, Span<float> pressure, Span<float> windX, Span<float> windY ) {
			Events.Add( 'P' );
			_wind.Publish( presence, windScale, temperature, flowEast, flowSouth, pressure, windX, windY );
		}
	}

	private readonly record struct Cell( bool Permeable, float Gas, float Condensate, float GasTemperature, float StructureTemperature );

	private readonly record struct Strategy( Harness Harness ) : IAtmosphereCellStrategy<Cell>, IThermalCellStrategy<Cell> {
		public bool IsPermeable( in Cell cell ) => cell.Permeable;
		public float GetGas( in Cell cell, GasIndex gas ) => cell.Gas;
		public void SetGas( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Gas = amount };
		public float GetCondensate( in Cell cell, GasIndex gas ) => cell.Condensate;
		public void SetCondensate( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Condensate = amount };
		public float GetTemperature( in Cell cell ) => cell.GasTemperature;
		public void SetTemperature( ref Cell cell, float temperature ) => cell = cell with { GasTemperature = temperature };
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => cell.StructureTemperature;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { StructureTemperature = temperature };
		public MaterialIndex GetMaterial( in Cell cell ) {
			if( Harness.MaterialReads == 0 ) {
				throw new InvalidOperationException( "Expected metadata extraction failure." );
			}
			Harness.MaterialReads--;
			return Harness.Material;
		}
	}

	private sealed class TestClock : ISimulationClock {
		public float FixedTimeStep { get; set; }
	}

	private sealed class Settings( int atmosphereCap, int thermalCap, float interval ) : IAtmosphericsSettings, IThermalsSettings, IAtmosphericThermalsSettings {
		public float Acceleration => 2.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.0f;
		public float HeatCapacity => 20.0f;
		public float CondensationRate => 0.0f;
		int IAtmosphericsSettings.MaxStepsPerUpdate => atmosphereCap;
		int IThermalsSettings.MaxStepsPerUpdate => thermalCap;
		public float WindScale => 1.0f;
		public float StefanBoltzmann => 0.0f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 0.0f;
		public float ExchangeInterval => interval;
	}

	private sealed class GasSource : IGasDefinitionSource {
		public IEnumerable<GasDefinition> GetDefinitions() => [new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f )];
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() => [new MaterialDefinition( "hull", "Hull", Conductivity: 0.0f, HeatCapacity: 1000.0f, Emissivity: 0.0f, Absorptivity: 0.0f, Convection: 10.0f )];
	}

}
