using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics.UnitTests;

namespace Kiyote.Simulations.LowFidelity.Thermals.UnitTests;

[TestFixture]
public sealed class ThermalRadiationTests {

	private const int X = 2;
	private const int Y = 3;
	private const float StefanBoltzmann = 5.67e-8f;
	private const float Space = 2.7f;

	private IThermalRadiation _radiation;
	private ChunkBuffer<float> _emissivity;
	private ChunkBuffer<float> _capacity;
	private ChunkBuffer<float> _solar;
	private ChunkBuffer<Direction> _vacuum;
	private ChunkBuffer<float> _temperature;

	[SetUp]
	public void SetUp() {
		_radiation = new ThermalRadiation();
		_emissivity = ChunkBuffer<float>.Chunk();
		_emissivity.Fill( 1.0f );
		_capacity = ChunkBuffer<float>.Chunk();
		_capacity.Fill( 1000.0f );
		_solar = ChunkBuffer<float>.Chunk();
		_vacuum = ChunkBuffer<Direction>.Chunk();
		_temperature = ChunkBuffer<float>.Chunk();
		_temperature.Fill( 300.0f );
	}

	[Test]
	public void Radiate_VacuumFace_Cools() {
		_vacuum[X, Y] = Direction.North;

		Radiate();

		Assert.That( _temperature[X, Y], Is.LessThan( 300.0f ).And.GreaterThan( 299.0f ) );
	}

	[Test]
	public void Radiate_NoVacuumFace_IsUnchanged() {
		Radiate();

		Assert.That( _temperature[X, Y], Is.EqualTo( 300.0f ) );
	}

	[Test]
	public void Radiate_Sunlight_Heats() {
		_vacuum[X, Y] = Direction.East;
		_emissivity[X, Y] = 0.0f;
		_solar[X, Y] = 1000.0f;

		Radiate();

		Assert.That( _temperature[X, Y], Is.EqualTo( 301.0f ).Within( 1e-3f ) );
	}

	[Test]
	public void Radiate_LargeLoss_NeverCoolsBelowSpace() {
		_vacuum[X, Y] = Direction.North | Direction.East | Direction.South | Direction.West;
		_capacity[X, Y] = 1e-6f;

		Radiate();

		Assert.That( _temperature[X, Y], Is.EqualTo( Space ) );
	}

	private void Radiate() {
		_radiation.Radiate(
			ChunkMask.Cell( X, Y ),
			1.0f,
			StefanBoltzmann,
			Space,
			_emissivity.Span,
			_capacity.Span,
			_solar.Span,
			_vacuum.Span,
			_temperature.Span
		);
	}

}
