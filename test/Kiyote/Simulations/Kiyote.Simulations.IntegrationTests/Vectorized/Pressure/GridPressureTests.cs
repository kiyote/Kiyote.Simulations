using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Pressure;

namespace Kiyote.Simulations.Vectorized.Pressure.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridPressureTests {

	private const float Rate = 1f;

	private readonly ISimulationClock _clock;
	private readonly IFieldCompiler _compiler;
	private readonly IGridPressure _pressure;

	public GridPressureTests() {
		_clock = new SimulationClock();
		_compiler = new FieldCompiler();
		_pressure = new GridPressure( new Settings( Rate ), _clock );
	}

	[Test]
	public void Update_OneStep_TransferScaledByTimeStep() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[5, 5] = 1000f;
		GridTopology<float> topology = Build( new OpenFloatConnectivityStrategy(), grid );

		IGrid<float> result = Step( topology, grid, 1 );

		float transfer = Rate * _clock.FixedTimeStep * 1000f;
		using( Assert.EnterMultipleScope() ) {
			Assert.That( result[5, 5], Is.EqualTo( 1000f - ( 8 * transfer ) ).Within( 0.001f ) );
			Assert.That( result[4, 4], Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( result[5, 4], Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( result[6, 6], Is.EqualTo( transfer ).Within( 0.001f ) );
			Assert.That( result[3, 5], Is.Zero );
		}
	}

	[Test]
	public void Update_ManySteps_TotalConserved() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[1, 1] = 1000f;
		cells[8, 6] = 500f;
		GridTopology<float> topology = Build( new OpenFloatConnectivityStrategy(), grid );

		IGrid<float> result = Step( topology, grid, 200 );

		Assert.That( Sum( result ), Is.EqualTo( 1500f ).Within( 0.05f ) );
	}

	[Test]
	public void Update_ManySteps_Equalises() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[5, 5] = 1000f;
		GridTopology<float> topology = Build( new OpenFloatConnectivityStrategy(), grid );

		IGrid<float> result = Step( topology, grid, 2000 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( result[0, 0], Is.EqualTo( 10f ).Within( 0.5f ) );
			Assert.That( result[9, 9], Is.EqualTo( 10f ).Within( 0.5f ) );
			Assert.That( result[5, 5], Is.EqualTo( 10f ).Within( 0.5f ) );
		}
	}

	[Test]
	public void Update_BoundaryConnectivity_WallsUnchanged() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[5, 5] = 1000f;
		cells[3, 3] = 50f;
		// Walls on the ring of the 5x5 rectangle at (3,3); interior 4-6.
		GridTopology<float> topology = Build( new BoundaryFloatConnectivityStrategy( 3, 3, 5, 5 ), grid );

		IGrid<float> result = Step( topology, grid, 200 );

		float inside = 0f;
		for( int row = 4; row <= 6; row++ ) {
			for( int column = 4; column <= 6; column++ ) {
				inside += result[column, row];
			}
		}
		using( Assert.EnterMultipleScope() ) {
			Assert.That( inside, Is.EqualTo( 1000f ).Within( 0.05f ) );
			Assert.That( result[3, 3], Is.EqualTo( 50f ) );
			Assert.That( result[2, 2], Is.Zero );
		}
	}

	private IGrid<float> Step(
		GridTopology<float> topology,
		RaggedArrayGrid<float> grid,
		int steps
	) {
		Field<float> source = _compiler.Compile<float, Identity>( topology, default );
		Field<float> destination = new Field<float>( topology );
		for( int i = 0; i < steps; i++ ) {
			_pressure.Update( source, destination );
			( source, destination ) = ( destination, source );
		}
		_compiler.Decompile<float, Identity>( source, default );
		return grid;
	}

	private static GridTopology<float> Build(
		IConnectivityStrategy<float> strategy,
		RaggedArrayGrid<float> grid
	) {
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		Assert.That( connectivity.TryAttach( grid, 0, 0 ), Is.True );
		connectivity.UpdateConnectivity( strategy );
		return connectivity.BuildTopology();
	}

	private static float Sum(
		IGrid<float> grid
	) {
		float total = 0f;
		for( int row = grid.Row; row < grid.Row + grid.Height; row++ ) {
			for( int column = grid.Column; column < grid.Column + grid.Width; column++ ) {
				total += grid[column, row];
			}
		}
		return total;
	}

	private readonly struct Identity : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}

	private sealed class Settings : IGridPressureSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

}
