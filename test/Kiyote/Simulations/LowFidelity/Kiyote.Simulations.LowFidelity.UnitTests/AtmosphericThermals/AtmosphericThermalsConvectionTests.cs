using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;
using Kiyote.Simulations.LowFidelity.Thermals;
using Moq;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals.UnitTests;

[TestFixture]
internal sealed class AtmosphericThermalsConvectionTests {

	private const float GasHeatCapacity = 1000.0f;

	private IAtmosphericThermalsConvection _convection;
	private Mock<IAtmosphereConvectionState> _atmosphere;
	private Mock<IThermalConvectionState> _thermal;
	private ICompiledGridAssembly<float> _compiled;
	private IGridLayer<float> _gasTemperature;
	private IGridLayer<float> _structureTemperature;

	[SetUp]
	public void SetUp() {
		_convection = new AtmosphericThermalsConvection();
		_compiled = TestGridAssembly.Create( 2, 1 );
		_gasTemperature = _compiled.CreateLayer<float>();
		_structureTemperature = _compiled.CreateLayer<float>();
		for( int column = 0; column < 2; column++ ) {
			_gasTemperature[column, 0] = 300.0f;
			_structureTemperature[column, 0] = 290.0f;
		}
		_atmosphere = new Mock<IAtmosphereConvectionState>( MockBehavior.Strict );
		_atmosphere.Setup( a => a.Temperature ).Returns( _gasTemperature );
		_atmosphere.Setup( a => a.GetTotalGasAt( 0, 0 ) ).Returns( 1.0f );
		_atmosphere
			.Setup( a => a.AddEnergyAt( 0, 0, GasHeatCapacity, It.IsAny<float>() ) )
			.Returns( ( int _, int _, float _, float joules ) => joules );
		_thermal = new Mock<IThermalConvectionState>( MockBehavior.Strict );
		_thermal.Setup( t => t.Temperature ).Returns( _structureTemperature );
		_thermal.Setup( t => t.AddEnergyAt( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>() ) );
	}

	[TearDown]
	public void TearDown() {
		_compiled.Dispose();
	}

	[Test]
	public void Exchange_SmallStep_MovesConductanceTimesDifference() {
		ConvectionCell[] cells = [new ConvectionCell( 0, 0, 0, 1 )];
		ConvectionLink[] links = [new ConvectionLink( 0, 1, 10.0f, 1000000.0f )];

		_convection.Exchange( cells, links, 1.0f, GasHeatCapacity, _atmosphere.Object, _thermal.Object );

		_atmosphere.Verify( a => a.AddEnergyAt( 0, 0, GasHeatCapacity, -100.0f ), Times.Once );
		_thermal.Verify( t => t.AddEnergyAt( 0, 1, 1000000.0f, 100.0f ), Times.Once );
	}

	[Test]
	public void Exchange_LargeStep_CappedAtSharedEquilibrium() {
		ConvectionCell[] cells = [new ConvectionCell( 0, 0, 0, 1 )];
		ConvectionLink[] links = [new ConvectionLink( 0, 1, 10.0f, 1000.0f )];
		// Equal capacities meet half way: 5K * 1000 J/K.
		const float Expected = -5000.0f;

		_convection.Exchange( cells, links, 1000.0f, GasHeatCapacity, _atmosphere.Object, _thermal.Object );

		_atmosphere.Verify( a => a.AddEnergyAt( 0, 0, GasHeatCapacity, It.Is<float>( j => MathF.Abs( j - Expected ) < 0.01f ) ), Times.Once );
		_thermal.Verify( t => t.AddEnergyAt( 0, 1, 1000.0f, It.Is<float>( j => MathF.Abs( j + Expected ) < 0.01f ) ), Times.Once );
	}

	[Test]
	public void Exchange_SeveralFaces_EachCappedAtAnEqualShare() {
		ConvectionCell[] cells = [new ConvectionCell( 0, 0, 0, 2 )];
		ConvectionLink[] links = [
			new ConvectionLink( 0, 0, 10.0f, 1000.0f ),
			new ConvectionLink( 0, 1, 10.0f, 1000.0f )
		];
		const float Expected = -2500.0f;

		_convection.Exchange( cells, links, 1000.0f, GasHeatCapacity, _atmosphere.Object, _thermal.Object );

		_atmosphere.Verify( a => a.AddEnergyAt( 0, 0, GasHeatCapacity, It.Is<float>( j => MathF.Abs( j - Expected ) < 0.01f ) ), Times.Exactly( 2 ) );
		_thermal.Verify( t => t.AddEnergyAt( 0, 0, 1000.0f, It.Is<float>( j => MathF.Abs( j + Expected ) < 0.01f ) ), Times.Once );
		_thermal.Verify( t => t.AddEnergyAt( 0, 1, 1000.0f, It.Is<float>( j => MathF.Abs( j + Expected ) < 0.01f ) ), Times.Once );
	}

	[Test]
	public void Exchange_AtmosphereAppliesLess_StructureGetsOnlyWhatWasApplied() {
		ConvectionCell[] cells = [new ConvectionCell( 0, 0, 0, 1 )];
		ConvectionLink[] links = [new ConvectionLink( 0, 1, 10.0f, 1000000.0f )];
		_atmosphere.Setup( a => a.AddEnergyAt( 0, 0, GasHeatCapacity, It.IsAny<float>() ) ).Returns( -40.0f );

		_convection.Exchange( cells, links, 1.0f, GasHeatCapacity, _atmosphere.Object, _thermal.Object );

		_thermal.Verify( t => t.AddEnergyAt( 0, 1, 1000000.0f, 40.0f ), Times.Once );
	}

	[Test]
	public void Exchange_NoGas_NothingMoves() {
		ConvectionCell[] cells = [new ConvectionCell( 0, 0, 0, 1 )];
		ConvectionLink[] links = [new ConvectionLink( 0, 1, 10.0f, 1000.0f )];
		_atmosphere.Setup( a => a.GetTotalGasAt( 0, 0 ) ).Returns( 0.0f );

		_convection.Exchange( cells, links, 1.0f, GasHeatCapacity, _atmosphere.Object, _thermal.Object );

		_atmosphere.Verify( a => a.AddEnergyAt( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>() ), Times.Never );
		_thermal.Verify( t => t.AddEnergyAt( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<float>() ), Times.Never );
	}

}
