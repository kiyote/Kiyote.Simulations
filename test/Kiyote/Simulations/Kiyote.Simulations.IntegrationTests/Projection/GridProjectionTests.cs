using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.Pressure;
using Kiyote.Simulations.IntegrationTests;

namespace Kiyote.Simulations.Projection.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridProjectionTests {

	private readonly ISimulationClock _clock;
	private readonly IGridDiffusion _diffusion;
	private readonly IGridPressure _pressure;
	private readonly IGridProjection _projection;
	private readonly FloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatProjectionStrategy _floatProjectionStrategy;

	private IMutableGrid<Velocity> _sourceVelocity;
	private IMutableGrid<Velocity> _destinationVelocity;
	private IMutableGrid<float> _sourcePressure;
	private IMutableGrid<float> _destinationPressure;
	private IConnectivityGrid<float> _connectivity;

	public GridProjectionTests() {
		_connectivityStrategy = new FloatConnectivityStrategy();
		_floatProjectionStrategy = new FloatProjectionStrategy();
		_clock = new SimulationClock();
		_diffusion = new GridDiffusion();
		_pressure = new GridPressure( _diffusion, _clock );
		_projection = new GridProjection( _pressure );
	}

	[SetUp]
	public void SetUp() {
		_sourceVelocity = new ArrayGrid<Velocity>( 10, 10 );
		_destinationVelocity = new ArrayGrid<Velocity>( 10, 10 );
		_sourcePressure = new ArrayGrid<float>( 10, 10 );
		_destinationPressure = new ArrayGrid<float>( 10, 10 );

		_connectivity = new ConnectivityGrid<float>();
		_connectivity.TryAttach( _sourcePressure, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );
	}

	[Test]
	public void Update_SeededPressureAndVelocity_CorrectFieldCalculated() {
		// Arrange
		_sourceVelocity[2, 5] = new Velocity( 1.0f, 0.0f );
		_sourcePressure[2, 5] = 1000f;

		// Act
		_projection.Update( _sourceVelocity, _connectivity, _destinationVelocity, _sourcePressure, _destinationPressure, _floatProjectionStrategy );

		// Assert
		using( Assert.EnterMultipleScope() ) {
			// The seeded cell's velocity should be adjusted by the pressure gradient
			// produced from its own seeded pressure value.
			Assert.That( _destinationVelocity[2, 5], Is.Not.EqualTo( _sourceVelocity[2, 5] ) );

			// Cells far from the seeded location receive only a negligible pressure
			// signal after relaxation, so their velocity should be effectively unchanged.
			AssertVelocityUnchanged( 9, 9 );
			AssertVelocityUnchanged( 0, 0 );
		}
	}

	private void AssertVelocityUnchanged(
		int column,
		int row
	) {
		Velocity expected = _sourceVelocity[column, row];
		Velocity actual = _destinationVelocity[column, row];

		Assert.That( actual.X, Is.EqualTo( expected.X ).Within( 0.01f ) );
		Assert.That( actual.Y, Is.EqualTo( expected.Y ).Within( 0.01f ) );
	}
}
