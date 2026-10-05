using Moq;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereCondensationTests {

	private const int X = 2;
	private const int Y = 3;
	private const float Point = 90.0f;
	private const float Rate = 0.1f;

	private IAtmosphereCondensation _condensation;
	private Mock<IGasRegistry> _gases;
	private ChunkLayer<float> _temperatureNext;
	private ChunkLayer<float>[] _gasNext;
	private ChunkLayer<float> _condensate;
	private ChunkLayer<float>[] _condensates;

	[SetUp]
	public void SetUp() {
		_condensation = new AtmosphereCondensation();
		_gases = new Mock<IGasRegistry>( MockBehavior.Strict );
		_gases
			.Setup( g => g.GetDefinition( new GasIndex( 0 ) ) )
			.Returns( new GasDefinition( "O2", "Oxygen", Point, 50.0f ) );
		_temperatureNext = new ChunkLayer<float>();
		_gasNext = [new ChunkLayer<float>(), new ChunkLayer<float>()];
		_condensate = new ChunkLayer<float>();
		_condensates = [_condensate, null];
	}

	[Test]
	public void Condense_BelowPoint_MovesGasToCondensate() {
		_temperatureNext[X, Y] = Point - 10.0f;
		_gasNext[0][X, Y] = 10.0f;

		bool changing = Condense();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.True );
			Assert.That( _gasNext[0][X, Y], Is.EqualTo( 9.0f ).Within( 1e-5f ) );
			Assert.That( _condensate[X, Y], Is.EqualTo( 1.0f ).Within( 1e-5f ) );
			Assert.That( _condensate.Dirty, Is.True );
		}
	}

	[Test]
	public void Condense_AbovePoint_EvaporatesCondensate() {
		_temperatureNext[X, Y] = Point + 10.0f;
		_condensate[X, Y] = 5.0f;

		bool changing = Condense();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.True );
			Assert.That( _gasNext[0][X, Y], Is.EqualTo( 0.5f ).Within( 1e-5f ) );
			Assert.That( _condensate[X, Y], Is.EqualTo( 4.5f ).Within( 1e-5f ) );
		}
	}

	[Test]
	public void Condense_AtPoint_DoesNothing() {
		_temperatureNext[X, Y] = Point;
		_gasNext[0][X, Y] = 10.0f;
		_condensate[X, Y] = 5.0f;

		bool changing = Condense();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.False );
			Assert.That( _condensate.Dirty, Is.False );
		}
	}

	[Test]
	public void Condense_GasWithoutCondensateLayer_IsNotQueried() {
		_temperatureNext[X, Y] = 10.0f;
		_gasNext[1][X, Y] = 10.0f;

		Condense();

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _gasNext[1][X, Y], Is.EqualTo( 10.0f ) );
			_gases.Verify( g => g.GetDefinition( new GasIndex( 1 ) ), Times.Never );
		}
	}

	private bool Condense() {
		return _condensation.Condense( 0, ChunkMask.Cell( X, Y ), Rate, _gases.Object, _temperatureNext, _gasNext, _condensates );
	}

}
