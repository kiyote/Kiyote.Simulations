using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Advection;

namespace Kiyote.Simulations.IntegrationTests.Advection;

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class GridAdvectionTests {

	private readonly ISimulationClock _clock;
	private readonly IGridAdvection _advection;
	private readonly FloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatBilinearSampler _sampler;
	private IConnectivityGrid<float> _connectivity;
	private IMutableGrid<Velocity> _velocityGrid;
	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;

	public GridAdvectionTests() {
		_clock = new SimulationClock();
		_advection = new GridAdvection( _clock );
		_connectivityStrategy = new FloatConnectivityStrategy();
		_sampler = new FloatBilinearSampler();
	}

	[SetUp]
	public void SetUp() {
		_connectivity = new ConnectivityGrid<float>();
		_velocityGrid = new ArrayGrid<Velocity>( 10, 10 );
		_input = new ArrayGrid<float>( 10, 10 );
		_output = new ArrayGrid<float>( 10, 10 );

		_connectivity.TryAttach( _input, 0, 0 );
		_connectivity.UpdateConnectivity( _connectivityStrategy );
	}

	[Test]
	public void Update_SeededGrid_AdvectionCalculated() {
		// A uniform rightward wind blows across the whole grid so the seeded
		// concentration is carried downstream rather than sitting in an otherwise
		// motionless field - representative of a gas plume being pushed by airflow.
		for( int row = 0; row < 10; row++ ) {
			for( int column = 0; column < 10; column++ ) {
				_velocityGrid[column, row] = new Velocity( 10, 0 );
			}
		}
		_input[3, 5] = 1000f;
		_input[6, 2] = 500f;

		_advection.Update( _connectivity, _velocityGrid, _input, _output, _sampler );

		// Semi-Lagrangian advection is destination-driven: each destination cell traces
		// backward along its own velocity to find where its value came from. With a
		// uniform (10, 0) velocity and a fixed time step of 0.1f, every destination cell
		// (column, row) traces back to (column - 1, row), so each concentration should
		// appear shifted one cell to the right of where it was seeded.
		Assert.That( _output[4, 5], Is.EqualTo( 1000f ).Within( 0.0001f ) );
		Assert.That( _output[7, 2], Is.EqualTo( 500f ).Within( 0.0001f ) );

		// The cells the concentrations were seeded at are now empty, since the wind
		// carried their values downstream instead of leaving them in place.
		Assert.That( _output[3, 5], Is.EqualTo( 0f ).Within( 0.0001f ) );
		Assert.That( _output[6, 2], Is.EqualTo( 0f ).Within( 0.0001f ) );

		// Everywhere else along the same rows stays at the field's background value of
		// zero, confirming the plumes aren't smeared or duplicated elsewhere.
		for( int row = 0; row < 10; row++ ) {
			for( int column = 0; column < 10; column++ ) {
				if( ( column == 4 && row == 5 ) || ( column == 7 && row == 2 ) ) {
					continue;
				}
				Assert.That( _output[column, row], Is.EqualTo( 0f ).Within( 0.0001f ), $"Cell ({column}, {row}) was expected to be 0." );
			}
		}
	}
}
