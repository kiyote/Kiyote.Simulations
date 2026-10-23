using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.IntegrationTests.AtmosphericThermals;

[TestFixture]
internal sealed class ConvectionStorageTests {

	private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds( 100 );
	private readonly List<(int Column, int Row, DenseGridSource<Cell> Source, PlacementId Placement)> _cells = [];
	private ServiceProvider _services;
	private IGridAssembly<Cell> _assembly;
	private ICompiledGridAssembly<Cell> _compiled;
	private IAtmosphericThermals _simulation;
	private CaptureConvection _convection;
	private GasIndex _nitrogen;
	private MaterialIndex _hull;

	[SetUp]
	public void SetUp() {
		_cells.Clear();
		_compiled = null;
		_simulation = null;
		_assembly = new GridAssembly<Cell>();
		_convection = new CaptureConvection();
		IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new GasSource()] ) ).Build();
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
		_nitrogen = gases.GetIndex( "N2" );
		_hull = materials.GetIndex( "hull" );
		Settings settings = new Settings();
		_services = new ServiceCollection()
			.AddSingleton( gases )
			.AddSingleton( materials )
			.AddSingleton<IAtmosphericsSettings>( settings )
			.AddSingleton<IThermalsSettings>( settings )
			.AddSingleton<IAtmosphericThermalsSettings>( settings )
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.AddSingleton<IAtmosphericThermalsConvection>( _convection )
			.BuildServiceProvider();
	}

	[TearDown]
	public void TearDown() {
		_simulation?.Dispose();
		_compiled?.Dispose();
		_services.Dispose();
	}

	[TestCase( 0, 0 )]
	[TestCase( 31, 31 )]
	[TestCase( -1, -1 )]
	[TestCase( -32, -32 )]
	[TestCase( -33, 31 )]
	[TestCase( 31, -33 )]
	[TestCase( 31, 0 )]
	[TestCase( 0, 31 )]
	[TestCase( -32, -1 )]
	[TestCase( -1, -32 )]
	public void Update_CardinalWallsAcrossChunks_UsesOnlyFloorAndCardinalFacesInOrder( int column, int row ) {
		AddCell( column, row );
		AddCell( column, row - 1, false );
		AddCell( column + 1, row, false );
		AddCell( column, row + 1, false );
		AddCell( column - 1, row, false );
		AddCell( column + 1, row - 1, false );
		Create();
		double before = GetEnergy();

		Assert.That( _simulation.Update( Tick ), Is.EqualTo( 1 ) );

		Assert.That( _convection.Cells, Has.Length.EqualTo( 1 ) );
		Assert.That( _convection.Cells[0].LinkCount, Is.EqualTo( 5 ) );
		Assert.That( _convection.Links.Select( face => (face.Chunk, face.Local) ), Is.EqualTo( new[] {
			Address( column, row ), Address( column, row - 1 ), Address( column + 1, row ),
			Address( column, row + 1 ), Address( column - 1, row )
		} ) );
		Assert.That( _simulation.Thermal.Temperature[column + 1, row - 1], Is.EqualTo( 200.0f ) );
		foreach( ConvectionLink face in _convection.Links ) {
			Assert.That( _simulation.Thermal.Temperature.GetValues( face.Chunk )[face.Local], Is.GreaterThan( 200.0f ).And.LessThan( 300.0f ) );
		}
		Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
	}

	[TestCase( int.MinValue, int.MinValue )]
	[TestCase( int.MaxValue, int.MaxValue )]
	[TestCase( int.MinValue, int.MaxValue )]
	[TestCase( int.MaxValue, int.MinValue )]
	public void Update_SignedCoordinateLimits_ExchangesWithTheFloorWithoutWrapping( int column, int row ) {
		AddCell( column, row );
		Create();
		double before = GetEnergy();

		_ = _simulation.Update( Tick );

		Assert.That( _convection.Cells, Has.Length.EqualTo( 1 ) );
		Assert.That( _convection.Links, Has.Length.EqualTo( 1 ) );
		Assert.That( (_convection.Links[0].Chunk, _convection.Links[0].Local), Is.EqualTo( Address( column, row ) ) );
		Assert.That( _simulation.Thermal.Temperature[column, row], Is.GreaterThan( 200.0f ) );
		Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
	}

	[Test]
	public void Update_DistantPlacements_TraversesOnlyAllocatedChunks() {
		AddCell( -1000000, -1000000 );
		AddCell( 0, 0 );
		AddCell( 1000000, 1000000 );
		Create();
		double before = GetEnergy();

		_ = _simulation.Update( Tick );

		Assert.That( _convection.Cells, Has.Length.EqualTo( 3 ) );
		Assert.That( _convection.Links, Has.Length.EqualTo( 3 ) );
		foreach( var cell in _cells ) {
			Assert.That( _simulation.Thermal.Temperature[cell.Column, cell.Row], Is.GreaterThan( 200.0f ) );
		}
		Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
	}

	[Test]
	public void Update_EmptyAllocatedChunk_DoesNotProduceLinks() {
		AddCell( 31, 31 );
		AddCell( 64, 64, false );
		Create();
		int empty = Address( 64, 64 ).Chunk;
		Assert.That( _assembly.TryRemoveCell( _cells[1].Placement, 0, 0 ), Is.True );
		Assert.That( _compiled.ChunkLayout.GetPopulatedCount( empty ), Is.Zero );
		_simulation.InvalidateTopology();

		_ = _simulation.Update( Tick );

		Assert.That( _convection.Cells, Has.Length.EqualTo( 1 ) );
		Assert.That( _convection.Links, Has.Length.EqualTo( 1 ) );
		Assert.That( _convection.Links[0].Chunk, Is.Not.EqualTo( empty ) );
	}

	[Test]
	public void Update_SharedWall_ReceivesHeatFromBothGasCells() {
		AddCell( 30, 0 );
		AddCell( 31, 0, false );
		AddCell( 32, 0 );
		Create();
		double before = GetEnergy();

		_ = _simulation.Update( Tick );

		(int chunk, int local) = Address( 31, 0 );
		Assert.That( _convection.Cells, Has.Length.EqualTo( 2 ) );
		Assert.That( _convection.Links.Count( face => face.Chunk == chunk && face.Local == local ), Is.EqualTo( 2 ) );
		Assert.That( _simulation.Thermal.Temperature[31, 0], Is.GreaterThan( 200.1f ) );
		Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
	}

	[Test]
	public void Update_PermeableNeighbour_IsNotAWallFace() {
		AddCell( 31, 0 );
		AddCell( 32, 0 );
		Create();

		_ = _simulation.Update( Tick );

		Assert.That( _convection.Cells, Has.Length.EqualTo( 2 ) );
		Assert.That( _convection.Cells.All( cell => cell.LinkCount == 1 ), Is.True );
		Assert.That( _convection.Links, Has.Length.EqualTo( 2 ) );
	}

	[Test]
	public void Update_UnsortedPlacements_UsesChunkThenAscendingLocalOrder() {
		AddCell( 64, 64 );
		AddCell( 31, 0 );
		AddCell( 0, 31 );
		AddCell( 1, 0 );
		Create();
		List<(int Chunk, int Local)> expected = [];
		foreach( int chunk in _compiled.ChunkLayout.EnumerateChunks() ) {
			for( int local = 0; local < 1024; local++ ) {
				if( ( _compiled.ChunkLayout.GetPresence( chunk )[local / 32] & ( 1UL << ( local % 32 ) ) ) != 0 ) {
					expected.Add( (chunk, local) );
				}
			}
		}

		_ = _simulation.Update( Tick );

		Assert.That( _convection.Cells.Select( cell => (cell.Chunk, cell.Local) ), Is.EqualTo( expected ) );
	}

	[Test]
	public void Update_RepeatedTemperatureSwaps_ConservesEnergyAndEquilibratesMonotonically() {
		AddCell( -1, -1 );
		Create();
		double before = GetEnergy();
		float gas = 300.0f;
		float structure = 200.0f;
		for( int i = 0; i < 8; i++ ) {
			_ = _simulation.Update( Tick );
			float nextGas = _simulation.Atmosphere.Temperature[-1, -1];
			float nextStructure = _simulation.Thermal.Temperature[-1, -1];
			Assert.That( nextGas, Is.LessThan( gas ).And.GreaterThan( nextStructure ) );
			Assert.That( nextStructure, Is.GreaterThan( structure ) );
			Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
			gas = nextGas;
			structure = nextStructure;
		}
	}

	[Test]
	public void Update_MassChangesAfterTransport_UsesCurrentGasCapacity() {
		AddCell( 0, 0 );
		Create();
		double before = 0.0;
		_convection.BeforeExchange = () => {
			Assert.That( _simulation.Atmosphere.RemoveGas( 0, 0, _nitrogen, 50.0f ), Is.EqualTo( 50.0f ) );
			before = GetEnergy();
		};

		_ = _simulation.Update( Tick );

		Assert.That( _simulation.Atmosphere.Temperature[0, 0], Is.EqualTo( 299.9f ).Within( 0.0001f ) );
		Assert.That( GetEnergy(), Is.EqualTo( before ).Within( 0.1 ) );
	}

	[Test]
	public void Update_AtGasTemperatureFloor_TransfersOnlyActuallyAppliedEnergy() {
		AddCell( 0, 0, gasTemperature: 2.7f, structureTemperature: 0.0f );
		Create();
		double before = GetEnergy();

		_ = _simulation.Update( Tick );

		Assert.That( _simulation.Atmosphere.Temperature[0, 0], Is.EqualTo( 2.7f ) );
		Assert.That( _simulation.Thermal.Temperature[0, 0], Is.Zero );
		Assert.That( GetEnergy(), Is.EqualTo( before ) );
	}

	[Test]
	public void Update_ConvectionAfterDirtyFlagsAreCleared_CommitsBothTemperaturesAndPublishesGas() {
		AddCell( 0, 0 );
		Create();
		_convection.BeforeExchange = _compiled.Commit;

		_ = _simulation.Update( Tick );
		_compiled.Commit();

		Cell saved = _cells[0].Source.GetCell( 0, 0 );
		Assert.That( saved.GasTemperature, Is.LessThan( 300.0f ) );
		Assert.That( saved.StructureTemperature, Is.GreaterThan( 200.0f ) );
		Assert.That( saved.GasTemperature, Is.EqualTo( _simulation.Atmosphere.Temperature[0, 0] ) );
		Assert.That( saved.StructureTemperature, Is.EqualTo( _simulation.Thermal.Temperature[0, 0] ) );
		using AtmosphereFrameLease frame = _simulation.Frames.AcquireLatest();
		SimulationFrameLayer temperature = frame.Temperature;
		Assert.That( temperature.TryGet( 0, 0, out float published ), Is.True );
		Assert.That( published, Is.EqualTo( saved.GasTemperature ) );
	}

	[Test]
	public void Update_WarmedSparseExchange_AllocatesNoManagedBytes() {
		AddCell( -1000, -1000 );
		AddCell( 1000, 1000 );
		Create();
		_convection.Record = false;
		for( int i = 0; i < 100; i++ ) {
			_ = _simulation.Update( Tick );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int i = 0; i < 100; i++ ) {
			_ = _simulation.Update( Tick );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private void AddCell( int column, int row, bool permeable = true, float gasTemperature = 300.0f, float structureTemperature = 200.0f ) {
		DenseGridSource<Cell> source = new DenseGridSource<Cell>( 1, 1 );
		Assert.That( source.TrySetCell( 0, 0, new Cell( permeable, permeable ? 100.0f : 0.0f, gasTemperature, structureTemperature ) ), Is.True );
		AttachResult attached = _assembly.TryAttach( source, column, row );
		Assert.That( attached.Succeeded, Is.True );
		_cells.Add( (column, row, source, attached.Placement) );
	}

	private void Create() {
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		Strategy strategy = new Strategy( _hull );
		_simulation = _services.GetRequiredService<IGridAtmosphericThermals>().Create( _compiled, strategy, strategy );
	}

	private (int Chunk, int Local) Address( int column, int row ) {
		Assert.That( _compiled.ChunkLayout.TryLocate( column, row, out int chunk, out int local ), Is.True );
		return (chunk, local);
	}

	private double GetEnergy() {
		double energy = 0.0;
		foreach( var cell in _cells ) {
			if( _compiled.ChunkLayout.IsPopulated( cell.Column, cell.Row ) ) {
				energy += 1000.0 * _simulation.Thermal.Temperature[cell.Column, cell.Row];
				energy += 20.0 * _simulation.Atmosphere.GetTotalGas( cell.Column, cell.Row ) * _simulation.Atmosphere.Temperature[cell.Column, cell.Row];
			}
		}
		return energy;
	}

	private sealed class CaptureConvection : IAtmosphericThermalsConvection {
		private readonly IAtmosphericThermalsConvection _inner = new AtmosphericThermalsConvection();
		public ConvectionCell[] Cells { get; private set; } = [];
		public ConvectionLink[] Links { get; private set; } = [];
		public bool Record { get; set; } = true;
		public Action BeforeExchange { get; set; }

		public void Exchange( ReadOnlySpan<ConvectionCell> cells, ReadOnlySpan<ConvectionLink> links, float dt, float gasHeatCapacity, IAtmosphereConvectionState atmosphere, IThermalConvectionState thermal ) {
			if( Record ) {
				Cells = cells.ToArray();
				Links = links.ToArray();
			}
			BeforeExchange?.Invoke();
			_inner.Exchange( cells, links, dt, gasHeatCapacity, atmosphere, thermal );
		}
	}

	private readonly record struct Cell( bool Permeable, float Gas, float GasTemperature, float StructureTemperature );

	private readonly record struct Strategy( MaterialIndex Material ) : IAtmosphereCellStrategy<Cell>, IThermalCellStrategy<Cell> {
		public bool IsPermeable( in Cell cell ) => cell.Permeable;
		public float GetGas( in Cell cell, GasIndex gas ) => cell.Gas;
		public void SetGas( ref Cell cell, GasIndex gas, float amount ) => cell = cell with { Gas = amount };
		public float GetCondensate( in Cell cell, GasIndex gas ) => 0.0f;
		public void SetCondensate( ref Cell cell, GasIndex gas, float amount ) { }
		public float GetTemperature( in Cell cell ) => cell.GasTemperature;
		public void SetTemperature( ref Cell cell, float temperature ) => cell = cell with { GasTemperature = temperature };
		float IThermalCellStrategy<Cell>.GetTemperature( in Cell cell ) => cell.StructureTemperature;
		void IThermalCellStrategy<Cell>.SetTemperature( ref Cell cell, float temperature ) => cell = cell with { StructureTemperature = temperature };
		public MaterialIndex GetMaterial( in Cell cell ) => Material;
	}

	private sealed class GasSource : IGasDefinitionSource {
		public IEnumerable<GasDefinition> GetDefinitions() => [new GasDefinition( "N2", "Nitrogen", null, null )];
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() => [
			new MaterialDefinition( "hull", "Hull", Conductivity: 0.0f, HeatCapacity: 1000.0f, Emissivity: 0.0f, Absorptivity: 0.0f, Convection: 10.0f )
		];
	}

	private sealed class Settings : IAtmosphericsSettings, IThermalsSettings, IAtmosphericThermalsSettings {
		public float Acceleration => 0.0f;
		public float Friction => 0.5f;
		public float Conduction => 0.0f;
		public float HeatCapacity => 20.0f;
		public float CondensationRate => 0.0f;
		public int MaxStepsPerUpdate => 1;
		public float WindScale => 1.0f;
		public float StefanBoltzmann => 0.0f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 0.0f;
		public float ExchangeInterval => 0.1f;
	}

}
