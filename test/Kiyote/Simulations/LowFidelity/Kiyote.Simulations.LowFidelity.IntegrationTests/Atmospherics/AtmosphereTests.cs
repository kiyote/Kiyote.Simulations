using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.IntegrationTests;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;

[TestFixture]
internal sealed class AtmosphereTests {

	private IAtmosphere _atmosphere;
	private TestCellStrategy _cellStrategy;

	[SetUp]
	public void SetUp() {
		IGasRegistryBuilder builder = new GasRegistryBuilder( [
			new TestGasDefinitionSource()
		] );
		IGasRegistry gasRegistry = builder.Build();
		_cellStrategy = new TestCellStrategy();
		DenseGridSource<TestCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		IGridAssembly<TestCell> assembly = new GridAssembly<TestCell>();
		Assert.That( assembly.TryAttach( ship, 0, 0 ).Succeeded, Is.True );
		IAtmosphericsSettings settings = new TestAtmosphericsSettings();
		IGridCompiler compiler = new GridCompiler();
		IConnectivityBuilder connectivityBuilder = new ConnectivityBuilder();
		IGridAtmospherics atmospherics = new GridAtmospherics( gasRegistry, settings, compiler, connectivityBuilder );
		_atmosphere = atmospherics.Create( assembly, _cellStrategy );
	}

	[TearDown]
	public void TearDown() {
		_atmosphere?.Dispose();
	}

	[Test]
	public void Advance_GasPumps_AtmosphereIsAdded() {
		GasIndex oxygen = _atmosphere.Gases.GetIndex( "O2" );
		GasIndex nitrogen = _atmosphere.Gases.GetIndex( "N2" );
		(int Column, int Row, GasIndex Gas)[] pumps = [
			( 18, 7, oxygen ),
			( 17, 7, nitrogen )
		];
		const float pumpRate = 10.0f; // amount per second
		TimeSpan frame = TimeSpan.FromMilliseconds( 100 );
		int frames = (int)( TimeSpan.FromSeconds( 10 ) / frame );

		for( int i = 0; i < frames; i++ ) {
			foreach( (int column, int row, GasIndex gas) in pumps ) {
				_atmosphere.AddGas( column, row, gas, pumpRate * (float)frame.TotalSeconds );
			}
			_ = _atmosphere.Advance( frame );
		}

		// Sample the interior of the room the pumps sit in (inside the X walls).
		float oxygenTotal = 0.0f;
		float nitrogenTotal = 0.0f;
		int cellsWithGas = 0;
		int cells = 0;
		for( int row = 1; row <= 12; row++ ) {
			for( int column = 1; column <= 18; column++ ) {
				cells++;
				oxygenTotal += _atmosphere.GetGas( oxygen )[column, row];
				nitrogenTotal += _atmosphere.GetGas( nitrogen )[column, row];
				if( _atmosphere.GetTotalGas( column, row ) > 0.0f ) {
					cellsWithGas++;
				}
			}
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( oxygenTotal, Is.GreaterThan( 0.0f ) );
			Assert.That( nitrogenTotal, Is.GreaterThan( 0.0f ) );
			Assert.That( cellsWithGas, Is.EqualTo( cells ), "Gas should have spread throughout the room." );
		}
	}
}
