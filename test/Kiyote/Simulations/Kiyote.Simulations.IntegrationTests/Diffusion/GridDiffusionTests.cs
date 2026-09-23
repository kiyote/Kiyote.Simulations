using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.IntegrationTests.Diffusion;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridDiffusionTests {

	private IMutableGrid<double> _input;
	private IMutableGrid<double> _output;
	private IConnectivityGrid<double> _connectivity;

	private readonly IGridDiffusion _diffusion;

	private readonly ConnectivityStrategy _strategy;
	private readonly TestFlowStrategy _flow;

	public GridDiffusionTests() {
		_strategy = new ConnectivityStrategy();
		_diffusion = new GridDiffusion();
		_flow = new TestFlowStrategy();
	}


	[SetUp]
	public void SetUp() {
		_connectivity = new ConnectivityGrid<double>();
		_input = new ArrayGrid<double>( 10, 10 );
		_output = new ArrayGrid<double>( 10, 10 );
		_connectivity.TryAttach( _input, 0, 0 );
		_connectivity.UpdateConnectivity( _strategy );
	}

	[Test]
	public void Update_OneStep_OutputUpdated() {
		_input[5, 5] = 1000d;

		_diffusion.Update<double, double, TestFlowStrategy>( _input, _connectivity, _output, _flow );

		// ConnectivityStrategy allows orthogonal moves whenever both ends exist and are
		// solid, and allows diagonal moves whenever at least one of the two orthogonal
		// cells forming the corner exists. Since (5,5) is fully interior, it is
		// connected to all 8 neighbors, each with a symmetric neighbor count of 8.
		// Each edge transfers (1000 - 0) / (8 + 1) = 1000/9 from (5,5) to the neighbor.
		const double expectedTransfer = 1000d / 9d;

		using( Assert.EnterMultipleScope() ) {
			Assert.That( _output[5, 5], Is.EqualTo( 1000d - ( 8 * expectedTransfer ) ).Within( 0.0001 ) );
			Assert.That( _output[4, 4], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[5, 4], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[6, 4], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[4, 5], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[6, 5], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[4, 6], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[5, 6], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );
			Assert.That( _output[6, 6], Is.EqualTo( expectedTransfer ).Within( 0.0001 ) );

			// Cells outside the 3x3 neighborhood centered on (5,5) receive no transfer.
			Assert.That( _output[0, 0], Is.EqualTo( 0d ) );
			Assert.That( _output[9, 9], Is.EqualTo( 0d ) );
			Assert.That( _output[3, 5], Is.EqualTo( 0d ) );
		}
	}

}
