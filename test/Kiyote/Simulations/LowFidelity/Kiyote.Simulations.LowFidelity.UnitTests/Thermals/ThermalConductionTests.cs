using Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

namespace Kiyote.Simulations.LowFidelity.Thermals.UnitTests;

[TestFixture]
public sealed class ThermalConductionTests {

	private const int X = 2;
	private const int Y = 3;

	private IThermalConduction _conduction;
	private ChunkBuffer<float> _temperature;
	private ChunkBuffer<float> _conductivity;
	private ChunkBuffer<float> _capacity;
	private ChunkBuffer<float> _next;

	[SetUp]
	public void SetUp() {
		_conduction = new ThermalConduction();
		_temperature = ChunkBuffer<float>.Padded();
		_temperature.Fill( 300.0f );
		_conductivity = ChunkBuffer<float>.Padded();
		_conductivity.Fill( 1.0f );
		_capacity = ChunkBuffer<float>.Padded();
		_capacity.Fill( 100.0f );
		_next = ChunkBuffer<float>.Chunk();
	}

	[Test]
	public void Conduct_HotNeighbour_GainsHeat() {
		// Series conductance 1 W/K over 100 K for 1 s into 100 J/K.
		_temperature[X + 1, Y] = 400.0f;

		Conduct( ChunkMask.Full() );

		Assert.That( _next[X, Y], Is.EqualTo( 301.0f ).Within( 1e-3f ) );
	}

	[Test]
	public void Conduct_HotNeighbourInWestChunk_GainsHeatAcrossBorder() {
		_temperature[-1, Y] = 400.0f;

		Conduct( ChunkMask.Cell( 0, Y ) );

		Assert.That( _next[0, Y], Is.EqualTo( 301.0f ).Within( 1e-3f ) );
	}

	[Test]
	public void Conduct_UnoccupiedNeighbour_ExchangesNothing() {
		_temperature[X + 1, Y] = 400.0f;
		_capacity[X + 1, Y] = 0.0f;

		Conduct( ChunkMask.Cell( X, Y ) );

		Assert.That( _next[X, Y], Is.EqualTo( 300.0f ) );
	}

	[Test]
	public void Conduct_CellOutsideMask_CopiesTemperature() {
		_temperature[X + 1, Y] = 400.0f;

		Conduct( ChunkMask.Cell( X, Y ) );

		Assert.That( _next[X + 1, Y], Is.EqualTo( 400.0f ) );
	}

	private void Conduct(
		ulong[] mask
	) {
		_conduction.Conduct( mask, 1.0f, _temperature.Span, _conductivity.Span, _capacity.Span, _next.Span );
	}

}
