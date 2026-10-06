using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;

namespace Kiyote.Simulations.LowFidelity.Thermals.IntegrationTests;

[TestFixture]
internal sealed class ThermalTests {

	private const float Initial = 293.15f;

	private IThermal _thermal;

	[SetUp]
	public void SetUp() {
		IMaterialRegistryBuilder builder = new MaterialRegistryBuilder( [new TestMaterialSource()] );
		IMaterialRegistry materials = builder.Build();
		// A solid 10x10 block of hull.
		DenseGridSource<TestCell> ship = new DenseGridSource<TestCell>( 10, 10 );
		for( int y = 0; y < 10; y++ ) {
			for( int x = 0; x < 10; x++ ) {
				_ = ship.TrySetCell( x, y, new TestCell( IsWalkable: false, IsGasPermeable: false, IsVacuum: false ) );
			}
		}
		IGridAssembly<TestCell> assembly = new GridAssembly<TestCell>();
		Assert.That( assembly.TryAttach( ship, 0, 0 ).Succeeded, Is.True );
		IServiceProvider services = new ServiceCollection()
			.AddSingleton( materials )
			.AddSingleton<IThermalsSettings, TestThermalsSettings>()
			.AddSingleton<IGridCompiler, GridCompiler>()
			.AddLowFidelitySimulations()
			.BuildServiceProvider();
		_thermal = services.GetRequiredService<IGridThermals>().Create( assembly, new TestStrategy( materials.GetIndex( "hull" ) ) );
	}

	[TearDown]
	public void TearDown() {
		_thermal?.Dispose();
	}

	[Test]
	public void Advance_SunToTheEast_EastFaceHeatsAndWestFaceDoesNot() {
		_thermal.SunDirection = new Vector( 1.0f, 0.0f );

		_ = _thermal.Update( TimeSpan.FromSeconds( 1 ) );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _thermal.Temperature[9, 5], Is.GreaterThan( Initial ) );
			Assert.That( _thermal.Temperature[0, 5], Is.LessThan( Initial ) );
		}
	}

	[Test]
	public void Advance_AddEnergy_HeatConductsToNeighbours() {
		_thermal.AddEnergy( 5, 5, 100000.0f );

		_ = _thermal.Update( TimeSpan.FromSeconds( 1 ) );

		Assert.That( _thermal.Temperature[6, 5], Is.GreaterThan( Initial ) );
	}

	private sealed class TestMaterialSource : IMaterialDefinitionSource {
		public IEnumerable<MaterialDefinition> GetDefinitions() {
			return [new MaterialDefinition( "hull", "Hull", Conductivity: 50.0f, HeatCapacity: 1000.0f, Emissivity: 0.8f, Absorptivity: 0.6f, Convection: 10.0f )];
		}
	}

	private sealed class TestThermalsSettings : IThermalsSettings {
		public float StefanBoltzmann => 5.67e-8f;
		public float SpaceTemperature => 2.7f;
		public float SolarFlux => 1361.0f;
		public int MaxStepsPerUpdate => 100;
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
			return Initial;
		}

		public void SetTemperature( ref TestCell cell, float temperature ) {
		}
	}

}
