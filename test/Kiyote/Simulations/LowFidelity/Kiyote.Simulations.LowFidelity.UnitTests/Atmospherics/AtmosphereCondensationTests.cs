namespace Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

[TestFixture]
public sealed class AtmosphereCondensationTests {

	private const int X = 2;
	private const int Y = 3;
	private const float Point = 90.0f;
	private const float Rate = 0.1f;

	private IAtmosphereCondensation _condensation;
	private ChunkBuffer<float> _temperatureNext;
	private ChunkBuffer<float> _gasNext;
	private ChunkBuffer<float> _condensate;

	[SetUp]
	public void SetUp() {
		_condensation = new AtmosphereCondensation();
		_temperatureNext = ChunkBuffer<float>.Chunk();
		_gasNext = ChunkBuffer<float>.Chunk();
		_condensate = ChunkBuffer<float>.Chunk();
	}

	[Test]
	public void Condense_BelowPoint_MovesGasToCondensate() {
		_temperatureNext[X, Y] = Point - 10.0f;
		_gasNext[X, Y] = 10.0f;

		bool changing = Condense( out bool modified );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.True );
			Assert.That( modified, Is.True );
			Assert.That( _gasNext[X, Y], Is.EqualTo( 9.0f ).Within( 1e-5f ) );
			Assert.That( _condensate[X, Y], Is.EqualTo( 1.0f ).Within( 1e-5f ) );
		}
	}

	[Test]
	public void Condense_AbovePoint_EvaporatesCondensate() {
		_temperatureNext[X, Y] = Point + 10.0f;
		_condensate[X, Y] = 5.0f;

		bool changing = Condense( out _ );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.True );
			Assert.That( _gasNext[X, Y], Is.EqualTo( 0.5f ).Within( 1e-5f ) );
			Assert.That( _condensate[X, Y], Is.EqualTo( 4.5f ).Within( 1e-5f ) );
		}
	}

	[Test]
	public void Condense_AtPoint_DoesNothing() {
		_temperatureNext[X, Y] = Point;
		_gasNext[X, Y] = 10.0f;
		_condensate[X, Y] = 5.0f;

		bool changing = Condense( out bool modified );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( changing, Is.False );
			Assert.That( modified, Is.False );
		}
	}

	[Test]
	public void Condense_CellOutsideMask_IsUntouched() {
		_temperatureNext[X + 1, Y] = Point - 10.0f;
		_gasNext[X + 1, Y] = 10.0f;

		Condense( out _ );

		Assert.That( _gasNext[X + 1, Y], Is.EqualTo( 10.0f ) );
	}

	private bool Condense(
		out bool modified
	) {
		return _condensation.Condense(
			ChunkMask.Cell( X, Y ),
			Rate,
			Point,
			_temperatureNext.Span,
			_gasNext.Span,
			_condensate.Span,
			out modified
		);
	}

}
