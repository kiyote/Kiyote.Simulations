using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;
using Kiyote.Simulations.LowFidelity.IntegrationTests;
using Kiyote.Simulations.LowFidelity.Thermals;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals.IntegrationTests;

[TestFixture]
internal sealed class AtmosphericThermalsTests {

	private const float GasTemperature = 300.0f;
	private const float StructureTemperature = 200.0f;

	private IAtmosphericThermals _simulation;

	[SetUp]
	public void SetUp() {
		IMaterialRegistry materials = ( (IMaterialRegistryBuilder)new MaterialRegistryBuilder( [new TestMaterialSource()] ) ).Build();
		IGasRegistry gases = ( (IGasRegistryBuilder)new GasRegistryBuilder( [new TestGasDefinitionSource()] ) ).Build();
		// A 5x5 room: a ring of walls around a 3x3 floor.
		DenseGridSource<TestCell> ship = new DenseGridSource<TestCell>( 5, 5 );
		for( int y = 0; y < 5; y++ ) {
			for( int x = 0; x < 5; x++ ) {
				bool wall = x == 0 || y == 0 || x == 4 || y == 4;
				_ = ship.TrySetCell( x, y, new TestCell( IsWalkable: !wall, IsGasPermeable: !wall, IsVacuum: false ) );
			}
		}
		IGridAssembly<TestCell> assembly = new GridAssembly<TestCell>();
		Assert.That( assembly.TryAttach( ship, 0, 0 ).Succeeded, Is.True );
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( materials )
			.AddSingleton( gases )
			.AddSingleton<IAtmosphericsSettings, TestAtmosphericsSettings>()
			.AddSingleton<IThermalsSettings, TestThermalsSettings>()
			.AddSingleton<IAtmosphericThermalsSettings, TestAtmosphericThermalsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddSingleton<IConnectivityBuilder, ConnectivityBuilder>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		_simulation = services.GetRequiredService<IGridAtmosphericThermals>().Create(
			assembly,
			new TestCellStrategy(),
			new TestStrategy( materials.GetIndex( "hull" ) )
		);
		GasIndex o2 = gases.GetIndex( "O2" );
		for( int y = 1; y < 4; y++ ) {
			for( int x = 1; x < 4; x++ ) {
				_simulation.Atmosphere.AddGas( x, y, o2, 1.0f, GasTemperature );
			}
		}
	}

	[TearDown]
	public void TearDown() {
		_simulation?.Dispose();
	}

	[Test]
	public void Update_ShorterThanInterval_NoExchange() {
		int exchanges = _simulation.Update( TimeSpan.FromSeconds( 1 ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( exchanges, Is.Zero );
			Assert.That( _simulation.Thermal.Temperature[0, 2], Is.EqualTo( StructureTemperature ).Within( 0.001f ) );
		}
	}

	[Test]
	public void Update_ReachesInterval_GasCoolsAndWallsWarm() {
		int exchanges = _simulation.Update( TimeSpan.FromSeconds( 5 ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( exchanges, Is.EqualTo( 1 ) );
			Assert.That( _simulation.Atmosphere.Temperature[2, 2], Is.LessThan( GasTemperature ) );
			Assert.That( _simulation.Thermal.Temperature[0, 2], Is.GreaterThan( StructureTemperature ) );
			Assert.That( _simulation.Thermal.Temperature[2, 2], Is.GreaterThan( StructureTemperature ) );
		}
	}

	[Test]
	public void Update_AccumulatesAcrossCalls_ExchangesWhenIntervalReached() {
		int first = _simulation.Update( TimeSpan.FromSeconds( 3 ) );
		int second = _simulation.Update( TimeSpan.FromSeconds( 3 ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( first, Is.Zero );
			Assert.That( second, Is.EqualTo( 1 ) );
		}
	}

	[Test]
	public void Update_CornerWall_NoGasNeighbour_Unchanged() {
		_ = _simulation.Update( TimeSpan.FromSeconds( 5 ) );

		Assert.That( _simulation.Thermal.Temperature[0, 0], Is.EqualTo( StructureTemperature ).Within( 0.001f ) );
	}

	private sealed class TestMaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() {
			return [new MaterialDefinition( "hull", "Hull", Conductivity: 0.0f, HeatCapacity: 1000.0f, Emissivity: 0.0f, Absorptivity: 0.0f, Convection: 10.0f )];
		}
	}

	private sealed class TestThermalsSettings : IThermalsSettings {
		public float StefanBoltzmann => 0.0f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 0.0f;
		public int MaxStepsPerUpdate => 100;
	}

	private sealed class TestAtmosphericThermalsSettings : IAtmosphericThermalsSettings {
		public float ExchangeInterval => 5.0f;
	}

	private readonly struct TestStrategy : IThermalCellStrategy<TestCell> {
		private readonly MaterialIndex _hull;

		public TestStrategy( MaterialIndex hull ) {
			_hull = hull;
		}

		public MaterialIndex GetMaterial( in TestCell cell ) {
			return _hull;
		}

		public float GetTemperature( in TestCell cell ) {
			return StructureTemperature;
		}

		public void SetTemperature( ref TestCell cell, float temperature ) {
		}
	}

}
