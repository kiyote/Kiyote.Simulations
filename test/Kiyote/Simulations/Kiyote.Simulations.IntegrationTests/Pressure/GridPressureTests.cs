using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;
using Kiyote.Simulations.IntegrationTests;

namespace Kiyote.Simulations.Pressure.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridPressureTests {

	private readonly IGridDiffusion _diffusion;
	private readonly IGridPressure _pressure;
	private readonly ISimulationClock _clock;

	private IConnectivityGrid<float> _connectivity;
	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;
	private readonly OpenFloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatPressureStrategy _pressureStrategy;

	public GridPressureTests() {
		_diffusion = new GridDiffusion();
		_clock = new SimulationClock();
		_pressure = new GridPressure( _diffusion, _clock );
		_connectivityStrategy = new OpenFloatConnectivityStrategy();
		_pressureStrategy = new FloatPressureStrategy();
	}

	[SetUp]
	public void SetUp() {
		_connectivity = new ConnectivityGrid<float>();
		_input = new ArrayGrid<float>( 10, 10 );
		_output = new ArrayGrid<float>( 10, 10 );
		_connectivity.TryAttach( _input, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );
	}

	[Test]
	public void Update_ShouldUpdateDestinationGrid_ValuesMatch() {
		// Arrange
		_input[5, 5] = 1000f;

		// Act
		_pressure.Update<float, float, float, FloatPressureStrategy>( _connectivity, _input, _output, _pressureStrategy );

		// ConnectivityStrategy allows orthogonal moves whenever both ends exist and are
		// solid, and allows diagonal moves whenever at least one of the two orthogonal
		// cells forming the corner exists. Since (5,5) is fully interior, it is
		// connected to all 8 neighbors, each with a symmetric neighbor count of 8.
		// Each edge transfers ( ( 1000 - 0 ) / ( 8 + 1 ) ) * FixedTimeStep from (5,5) to the neighbor.
		float expectedTransfer = 1000f / 9f * _clock.FixedTimeStep;

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _output[5, 5], Is.EqualTo( 1000f - ( 8 * expectedTransfer ) ).Within( 0.001f ) );
			Assert.That( _output[4, 4], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[5, 4], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[6, 4], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[4, 5], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[6, 5], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[4, 6], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[5, 6], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );
			Assert.That( _output[6, 6], Is.EqualTo( expectedTransfer ).Within( 0.001f ) );

			// Cells outside the 3x3 neighborhood centered on (5,5) receive no transfer.
			Assert.That( _output[0, 0], Is.EqualTo( 0f ) );
			Assert.That( _output[9, 9], Is.EqualTo( 0f ) );
			Assert.That( _output[3, 5], Is.EqualTo( 0f ) );
		}
	}
}
