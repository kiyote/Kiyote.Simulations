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
	private readonly OpenFloatConnectivityStrategy _openConnectivityStrategy;
	private readonly BoundaryFloatConnectivityStrategy _boundaryConnectivityStrategy;
	private readonly FloatProjectionStrategy _floatProjectionStrategy;

	private IMutableGrid<Velocity> _sourceVelocity;
	private IMutableGrid<Velocity> _destinationVelocity;
	private IMutableGrid<float> _sourcePressure;
	private IMutableGrid<float> _destinationPressure;
	private IConnectivityGrid<float> _openConnectivity;
	private IConnectivityGrid<float> _boundaryConnectivity;

	public GridProjectionTests() {
		_openConnectivityStrategy = new OpenFloatConnectivityStrategy();
		// Encloses a 3x3 interior box (columns/rows 4-6) inside the 10x10 grid, walled
		// off from the rest of the domain, so the seeded pressure/velocity below can
		// never reach cells outside the box - unlike OpenFloatConnectivityStrategy,
		// where only the domain's own outer edge reflects.
		_boundaryConnectivityStrategy = new BoundaryFloatConnectivityStrategy( 3, 3, 4, 4 );
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

		_openConnectivity = new ConnectivityGrid<float>();
		_openConnectivity.TryAttach( _sourcePressure, 0, 0 );
		_openConnectivity.UpdateConnectivity( _openConnectivityStrategy );

		_boundaryConnectivity = new ConnectivityGrid<float>();
		_boundaryConnectivity.TryAttach( _sourcePressure, 0, 0 );
		_boundaryConnectivity.UpdateConnectivity( _boundaryConnectivityStrategy );
	}

	[Test]
	public void Update_OpenConnectivity_CorrectFieldCalculated() {
		// Arrange
		_sourceVelocity[5, 5] = new Velocity( 1.0f, 0.0f );
		_sourcePressure[5, 5] = 1000f;

		// Act
		_projection.Update( _openConnectivity, _sourceVelocity, _destinationVelocity, _sourcePressure, _destinationPressure, _floatProjectionStrategy );

		// Assert
		using( Assert.EnterMultipleScope() ) {
			// The seeded cell's velocity should be adjusted by the pressure gradient
			// produced from its own seeded pressure value.
			Assert.That( _destinationVelocity[5, 5], Is.Not.EqualTo( _sourceVelocity[5, 5] ) );

			// Unlike the boundary-connectivity case below, nothing walls off cell (8, 5)
			// from the seeded cell, so after relaxation some (even if small) amount of
			// pressure signal reaches it, nudging its velocity away from its original value.
			Assert.That( _destinationVelocity[8, 5], Is.Not.EqualTo( _sourceVelocity[8, 5] ) );

			// Cells far from the seeded location receive only a negligible pressure
			// signal after relaxation, so their velocity should be effectively unchanged.
			AssertVelocityUnchanged( 9, 9 );
			AssertVelocityUnchanged( 0, 0 );
		}
	}

	[Test]
	public void Update_BoundaryConnectivity_CorrectFieldCalculated() {
		// Arrange
		_sourceVelocity[5, 5] = new Velocity( 1.0f, 0.0f );
		_sourcePressure[5, 5] = 1000f;

		// Act
		_projection.Update( _boundaryConnectivity, _sourceVelocity, _destinationVelocity, _sourcePressure, _destinationPressure, _floatProjectionStrategy );

		// Assert
		using( Assert.EnterMultipleScope() ) {
			// The seeded cell's velocity should be adjusted by the pressure gradient
			// produced from its own seeded pressure value.
			Assert.That( _destinationVelocity[5, 5], Is.Not.EqualTo( _sourceVelocity[5, 5] ) );

			// Cell (8, 5) sits outside the walled-off interior box the seeded cell lives
			// in, so it is fully reflected/isolated from the seeded pressure - unlike the
			// open-connectivity case above, its velocity should be exactly unchanged.
			AssertVelocityUnchanged( 8, 5 );

			// Cells far from the seeded location receive only a negligible pressure
			// signal after relaxation, so their velocity should be effectively unchanged.
			AssertVelocityUnchanged( 9, 9 );
			AssertVelocityUnchanged( 0, 0 );
		}
	}


	[Test]
	public void Update_BoundaryConnectivity_ImpassableCellsUnchanged() {
		// Arrange
		_sourceVelocity[5, 5] = new Velocity( 1.0f, 0.0f );
		_sourcePressure[5, 5] = 1000f;

		// Cells at/outside the walled-off interior box (columns/rows 3-7) are
		// impassable - seed a handful of them with a distinctive, non-zero value so
		// any drift (e.g. accumulation from repeated Update calls) would be detected,
		// rather than merely coincidentally matching an already-zeroed default.
		(int Column, int Row)[] wallCells = [
			( 0, 0 ),
			( 3, 3 ),
			( 3, 5 ),
			( 7, 5 ),
			( 9, 9 ),
		];
		foreach( (int column, int row) in wallCells ) {
			_sourcePressure[column, row] = 42f;
		}

		// Act - run several iterations, double-buffering source/destination the same
		// way a caller advancing the simulation over multiple frames would, so any
		// accumulation across calls (rather than just within a single call) would
		// also be caught.
		for( int i = 0; i < 5; i++ ) {
			_projection.Update( _boundaryConnectivity, _sourceVelocity, _destinationVelocity, _sourcePressure, _destinationPressure, _floatProjectionStrategy );
			( _sourceVelocity, _destinationVelocity ) = ( _destinationVelocity, _sourceVelocity );
			( _sourcePressure, _destinationPressure ) = ( _destinationPressure, _sourcePressure );
		}

		// Assert
		using( Assert.EnterMultipleScope() ) {
			foreach( (int column, int row) in wallCells ) {
				Assert.That( _sourcePressure[column, row], Is.EqualTo( 42f ), $"Cell ({column}, {row}) should remain unchanged." );
			}
		}
	}

	[Test]
	public void Update_BoundaryConnectivityVisualizerScenario_ImpassableCellsNeverWritten() {
		// Arrange - mirrors BoundaryGridProjectionVisualizer.Execute: a 100x100 grid
		// whose connectivity strategy walls off the domain's own outer edge (rather
		// than an interior box), seeded with the same two pressure/velocity points,
		// advanced the same number of frames with the same number of physics steps
		// per frame.
		const int size = 100;
		const int totalFrameCount = 100;
		const int stepsPerFrame = 4;

		BoundaryFloatConnectivityStrategy visualizerConnectivityStrategy = new( 0, 0, size, size );
		IConnectivityGrid<float> visualizerConnectivity = new ConnectivityGrid<float>();

		IMutableGrid<float> sourcePressure = new ArrayGrid<float>( size, size );
		IMutableGrid<float> destinationPressure = new ArrayGrid<float>( size, size );
		visualizerConnectivity.TryAttach( sourcePressure, 0, 0 );
		visualizerConnectivity.UpdateConnectivity( visualizerConnectivityStrategy );

		IMutableGrid<Velocity> sourceVelocity = new ArrayGrid<Velocity>( size, size );
		IMutableGrid<Velocity> destinationVelocity = new ArrayGrid<Velocity>( size, size );

		sourcePressure[95, 5] = 1000f;
		sourcePressure[50, 50] = 1000f;

		sourceVelocity[95, 5] = new Velocity( -10, -10 );
		sourceVelocity[50, 50] = new Velocity( 5, 0 );

		// A cell is impassable here whenever the connectivity strategy reports it has
		// no open direction at all - for BoundaryFloatConnectivityStrategy(0, 0, size,
		// size) that is precisely the domain's own outer edge (column/row 0 or size-1).
		List<(int Column, int Row)> impassableCells = [];
		for( int row = 0; row < size; row++ ) {
			for( int column = 0; column < size; column++ ) {
				if( visualizerConnectivity[column, row] == Direction.None ) {
					impassableCells.Add( ( column, row ) );
				}
			}
		}
		Assert.That( impassableCells, Is.Not.Empty );

		// Act & Assert - after every single physics step (matching the visualizer's
		// inner StepsPerFrame loop), verify none of the impassable cells were written
		// to with anything other than their untouched starting value.
		for( int frame = 0; frame < totalFrameCount; frame++ ) {
			for( int step = 0; step < stepsPerFrame; step++ ) {
				_projection.Update( visualizerConnectivity, sourceVelocity, destinationVelocity, sourcePressure, destinationPressure, _floatProjectionStrategy );
				( sourceVelocity, destinationVelocity ) = ( destinationVelocity, sourceVelocity );
				( sourcePressure, destinationPressure ) = ( destinationPressure, sourcePressure );

				using( Assert.EnterMultipleScope() ) {
					foreach( (int column, int row) in impassableCells ) {
						Assert.That( sourcePressure[column, row], Is.EqualTo( 0f ), $"Pressure at impassable cell ({column}, {row}) should remain unchanged after frame {frame}, step {step}." );
						Assert.That( sourceVelocity[column, row], Is.EqualTo( default( Velocity ) ), $"Velocity at impassable cell ({column}, {row}) should remain unchanged after frame {frame}, step {step}." );
					}
				}
			}
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
