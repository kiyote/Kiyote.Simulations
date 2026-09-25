using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;

namespace Kiyote.Simulations.Diffusion.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridDiffusionTests {

	private IMutableGrid<float> _input;
	private IMutableGrid<float> _output;
	private IConnectivityGrid<float> _openConnectivity;
	private IConnectivityGrid<float> _boundaryConnectivity;

	private readonly IGridDiffusion _diffusion;

	private readonly OpenFloatConnectivityStrategy _openConnectivityStrategy;
	private readonly BoundaryFloatConnectivityStrategy _boundaryConnectivityStrategy;
	private readonly FloatDiffusionStrategy _flow;

	public GridDiffusionTests() {
		_openConnectivityStrategy = new OpenFloatConnectivityStrategy();
		// Encloses a 3x3 interior box (columns/rows 4-6) inside the 10x10 grid, walled
		// off from the rest of the domain, so anything seeded inside can never diffuse
		// out to cells outside the box.
		_boundaryConnectivityStrategy = new BoundaryFloatConnectivityStrategy( 3, 3, 4, 4 );
		_diffusion = new GridDiffusion();
		_flow = new FloatDiffusionStrategy();
	}


	[SetUp]
	public void SetUp() {
		_openConnectivity = new ConnectivityGrid<float>();
		_input = new RaggedArrayGrid<float>( 10, 10 );
		_output = new RaggedArrayGrid<float>( 10, 10 );
		_openConnectivity.TryAttach( _input, 0, 0 );
		_openConnectivity.UpdateConnectivity( _openConnectivityStrategy );

		_boundaryConnectivity = new ConnectivityGrid<float>();
		_boundaryConnectivity.TryAttach( _input, 0, 0 );
		_boundaryConnectivity.UpdateConnectivity( _boundaryConnectivityStrategy );
	}

	[Test]
	public void Update_OneStep_OutputUpdated() {
		_input[5, 5] = 1000f;

		_diffusion.Update<float, float, float, FloatDiffusionStrategy>( _openConnectivity, _input, _output, _flow );

		// ConnectivityStrategy allows orthogonal moves whenever both ends exist and are
		// solid, and allows diagonal moves whenever at least one of the two orthogonal
		// cells forming the corner exists. Since (5,5) is fully interior, it is
		// connected to all 8 neighbors, each with a symmetric neighbor count of 8.
		// Each edge transfers (1000 - 0) / (8 + 1) = 1000/9 from (5,5) to the neighbor.
		const float expectedTransfer = 1000f / 9f;

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

	[Test]
	public void Update_BoundaryConnectivity_ImpassableCellsUnchanged() {
		// Arrange
		_input[5, 5] = 1000f;

		// Cells at/outside the walled-off interior box (columns/rows 3-7) are
		// impassable - seed a handful of them with a distinctive, non-zero value so
		// any diffusion into them (rather than merely coincidentally matching an
		// already-zeroed default) would be detected.
		(int Column, int Row)[] wallCells = [
			( 0, 0 ),
			( 3, 3 ),
			( 3, 5 ),
			( 7, 5 ),
			( 9, 9 ),
		];
		foreach( (int column, int row) in wallCells ) {
			_input[column, row] = 42f;
		}

		// Act
		_diffusion.Update<float, float, float, FloatDiffusionStrategy>( _boundaryConnectivity, _input, _output, _flow );

		// Assert
		using( Assert.EnterMultipleScope() ) {
			foreach( (int column, int row) in wallCells ) {
				Assert.That( _output[column, row], Is.EqualTo( 42f ), $"Cell ({column}, {row}) should remain unchanged." );
			}
		}
	}

}
