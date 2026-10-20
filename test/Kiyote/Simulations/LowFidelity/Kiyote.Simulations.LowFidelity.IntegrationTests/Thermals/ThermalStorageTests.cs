using Kiyote.Geometry.Topology;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Thermals.IntegrationTests;

[TestFixture]
internal sealed class ThermalStorageTests {

	private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds( 100 );
	private ServiceProvider _services;
	private DenseGridSource<Cell> _source;
	private IGridAssembly<Cell> _assembly;
	private ICompiledGridAssembly<Cell> _compiled;
	private IThermal _thermal;
	private Strategy _strategy;
	private Settings _settings;
	private PlacementId _placement;
	private CommitCounter _commits;
	private int _emptyChunk;

	[SetUp]
	public void SetUp() {
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new MaterialSource()] ) ).Build();
		_settings = new Settings();
		_services = new ServiceCollection()
			.AddSingleton( materials )
			.AddSingleton<IThermalsSettings>( _settings )
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		_source = new DenseGridSource<Cell>( 130, 1 );
		Assert.That( _source.TrySetCell( 0, 0, new Cell( 200.0f, 17 ) ), Is.True );
		Assert.That( _source.TrySetCell( 1, 0, new Cell( 400.0f, 23 ) ), Is.True );
		Assert.That( _source.TrySetCell( 129, 0, new Cell( 600.0f, 29 ) ), Is.True );
		_assembly = new GridAssembly<Cell>();
		AttachResult attached = _assembly.TryAttach( _source, -1, -32 );
		Assert.That( attached.Succeeded, Is.True );
		_placement = attached.Placement;
		_compiled = new GridCompiler().Compile( _assembly, 32 );
		Assert.That( _compiled.ChunkLayout.TryLocate( 128, -32, out _emptyChunk, out _ ), Is.True );
		Assert.That( _assembly.TryRemoveCell( _placement, 129, 0 ), Is.True );
		_commits = new CommitCounter();
		_strategy = new Strategy( materials.GetIndex( "hull" ), _commits );
		_thermal = _services.GetRequiredService<IGridThermals>().Create( _compiled, _strategy );
		_compiled.Commit();
		_commits.Writes = 0;
	}

	[TearDown]
	public void TearDown() {
		_thermal.Dispose();
		_compiled.Dispose();
		_services.Dispose();
	}

	[TestCase( 0, false )]
	[TestCase( 0, true )]
	[TestCase( 1, false )]
	[TestCase( 1, true )]
	[TestCase( 2, false )]
	[TestCase( 2, true )]
	[TestCase( 3, false )]
	[TestCase( 3, true )]
	[TestCase( 8, false )]
	[TestCase( 8, true )]
	public void Update_RepeatedSwaps_PreservesTemperatureBindingAndDirtyCommits( int ticks, bool commitEach ) {
		IGridLayer<float> temperature = _thermal.Temperature;
		float left = 200.0f;
		float right = 400.0f;
		for( int update = 0; update < 3; update++ ) {
			for( int tick = 0; tick < ticks; tick++ ) {
				Conduct( ref left, ref right );
			}

			Assert.That( _thermal.Update( TimeSpan.FromTicks( Tick.Ticks * ticks ) ), Is.EqualTo( ticks ) );

			Assert.That( _thermal.Temperature, Is.SameAs( temperature ) );
			Assert.That( _compiled.Layers, Does.Contain( temperature ) );
			AssertTemperatures( left, right );
			Assert.That( temperature.IsDirty( _emptyChunk ), Is.False );
			Assert.That( temperature.GetValues( _emptyChunk ).ToArray(), Is.All.Zero );
			if( commitEach ) {
				AssertCommit( left, right, ticks == 0 ? 0 : 2 );
			} else {
				Assert.That( _source.GetCell( 0, 0 ), Is.EqualTo( new Cell( 200.0f, 17 ) ) );
				Assert.That( _source.GetCell( 1, 0 ), Is.EqualTo( new Cell( 400.0f, 23 ) ) );
			}
		}
		if( !commitEach ) {
			AssertCommit( left, right, ticks == 0 ? 0 : 2 );
		}
		AssertCommit( left, right, 0 );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void Update_AddedEnergyBeforeSwapping_PreservesPhysicalState( bool commitBefore ) {
		_thermal.AddEnergy( -1, -32, 1000.0f );
		float left = 201.0f;
		float right = 400.0f;
		if( commitBefore ) {
			AssertCommit( left, right, 1 );
		}

		for( int tick = 0; tick < 3; tick++ ) {
			Conduct( ref left, ref right );
			Assert.That( _thermal.Update( Tick ), Is.EqualTo( 1 ) );
			AssertTemperatures( left, right );
		}

		AssertCommit( left, right, 2 );
		AssertCommit( left, right, 0 );
	}

	[Test]
	public void Update_ZeroTicksAfterAddingEnergy_KeepsPendingDirtyState() {
		_thermal.AddEnergy( -1, -32, 1000.0f );

		Assert.That( _thermal.Update( TimeSpan.Zero ), Is.Zero );

		AssertTemperatures( 201.0f, 400.0f );
		Assert.That( _source.GetCell( 0, 0 ), Is.EqualTo( new Cell( 200.0f, 17 ) ) );
		AssertCommit( 201.0f, 400.0f, 1 );
		AssertCommit( 201.0f, 400.0f, 0 );
	}

	[TestCase( false )]
	[TestCase( true )]
	public void Update_RadiationAfterConduction_CommitsFinalTemperature( bool solarHeating ) {
		if( solarHeating ) {
			_settings.SolarFlux = 1361.0f;
			_thermal.SunDirection = new Vector( 1.0f, 0.0f );
		} else {
			_settings.StefanBoltzmann = 5.67e-8f;
		}

		Assert.That( _thermal.Update( Tick ), Is.EqualTo( 1 ) );

		float left = _thermal.Temperature[-1, -32];
		float right = _thermal.Temperature[0, -32];
		Assert.That( right, solarHeating ? Is.GreaterThan( 399.0f ) : Is.LessThan( 399.0f ) );
		AssertCommit( left, right, 2 );
		AssertCommit( left, right, 0 );
	}

	[Test]
	public void Update_OnlyRetainedEmptyChunks_DoesNotCommitAnyCells() {
		Assert.That( _assembly.TryRemoveCell( _placement, 0, 0 ), Is.True );
		Assert.That( _assembly.TryRemoveCell( _placement, 1, 0 ), Is.True );
		_thermal.InvalidateTopology();
		_compiled.Commit();
		_commits.Writes = 0;

		for( int tick = 0; tick < 3; tick++ ) {
			Assert.That( _thermal.Update( Tick ), Is.EqualTo( 1 ) );
			foreach( int chunk in _compiled.ChunkLayout.EnumerateChunks() ) {
				Assert.That( _compiled.ChunkLayout.GetPopulatedCount( chunk ), Is.Zero );
				Assert.That( _thermal.Temperature.GetValues( chunk ).ToArray(), Is.All.Zero );
			}
			_compiled.Commit();
			Assert.That( _commits.Writes, Is.Zero );
		}
	}

	[Test]
	public void Update_EmptyCompilation_SwapsWithoutCreatingChunks() {
		using ICompiledGridAssembly<Cell> compiled = new GridCompiler().Compile( new GridAssembly<Cell>(), 32 );
		using IThermal thermal = _services.GetRequiredService<IGridThermals>().Create( compiled, _strategy );
		IGridLayer<float> temperature = thermal.Temperature;

		for( int tick = 0; tick < 3; tick++ ) {
			Assert.That( thermal.Update( Tick ), Is.EqualTo( 1 ) );
			compiled.Commit();
			Assert.That( thermal.Temperature, Is.SameAs( temperature ) );
			Assert.That( compiled.ChunkLayout.IsEmpty, Is.True );
			Assert.That( _commits.Writes, Is.Zero );
		}
	}

	[TestCase( false )]
	[TestCase( true )]
	public void Update_WarmedSwapsAndCommits_AllocateNoManagedBytes( bool commitEach ) {
		for( int tick = 0; tick < 100; tick++ ) {
			Advance( commitEach );
		}
		long before = GC.GetAllocatedBytesForCurrentThread();

		for( int tick = 0; tick < 100; tick++ ) {
			Advance( commitEach );
		}

		Assert.That( GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero );
	}

	private void Advance( bool commit ) {
		_thermal.AddEnergy( -1, -32, 1000.0f );
		_ = _thermal.Update( Tick );
		if( commit ) {
			_compiled.Commit();
		}
	}

	private static void Conduct( ref float left, ref float right ) {
		float change = 5.0f * ( right - left ) / 1000.0f;
		left += change;
		right -= change;
	}

	private void AssertTemperatures( float left, float right ) {
		Assert.That( _thermal.Temperature[-1, -32], Is.EqualTo( left ) );
		Assert.That( _thermal.Temperature[0, -32], Is.EqualTo( right ) );
	}

	private void AssertCommit( float left, float right, int writes ) {
		_commits.Writes = 0;
		_compiled.Commit();
		Assert.That( _commits.Writes, Is.EqualTo( writes ) );
		Assert.That( _source.GetCell( 0, 0 ), Is.EqualTo( new Cell( left, 17 ) ) );
		Assert.That( _source.GetCell( 1, 0 ), Is.EqualTo( new Cell( right, 23 ) ) );
	}

	private readonly record struct Cell( float Temperature, int Marker );

	private readonly record struct Strategy( MaterialIndex Material, CommitCounter Counter ) : IThermalCellStrategy<Cell> {
		public MaterialIndex GetMaterial( in Cell cell ) => Material;
		public float GetTemperature( in Cell cell ) => cell.Temperature;
		public void SetTemperature( ref Cell cell, float temperature ) {
			cell = cell with { Temperature = temperature };
			Counter.Writes++;
		}
	}

	private sealed class CommitCounter {
		public int Writes { get; set; }
	}

	private sealed class MaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() => [
			new MaterialDefinition( "hull", "Hull", Conductivity: 50.0f, HeatCapacity: 1000.0f, Emissivity: 0.8f, Absorptivity: 0.6f, Convection: 0.0f )
		];
	}

	private sealed class Settings : IThermalsSettings {
		public float StefanBoltzmann { get; set; }
		public float SpaceTemperature => 2.7f;
		public float SolarFlux { get; set; }
		public int MaxStepsPerUpdate => 8;
	}

}
