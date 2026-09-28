using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Diffusion;

namespace Kiyote.Simulations.Vectorized.Diffusion.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridDiffusionTests {

	private const float Rate = 0.1f;

	private readonly IFieldCompiler _compiler;
	private readonly IGridDiffusion _diffusion;

	public GridDiffusionTests() {
		_compiler = new FieldCompiler();
		_diffusion = new GridDiffusion( new Settings( Rate ) );
	}

	[Test]
	public void Update_OneStep_SpreadsToOpenNeighbours() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[5, 5] = 1000f;
		GridTopology<float> topology = Build( new OpenFloatConnectivityStrategy(), grid );

		IGrid<float> result = Step( topology, grid, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( result[5, 5], Is.EqualTo( 1000f - ( 8 * Rate * 1000f ) ).Within( 0.001f ) );
			Assert.That( result[4, 4], Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( result[6, 5], Is.EqualTo( Rate * 1000f ).Within( 0.001f ) );
			Assert.That( result[3, 3], Is.Zero );
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
	public void Update_BoundaryConnectivity_DoesNotLeaveBox() {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( 10, 10 );
		IMutableGrid<float> cells = grid;
		cells[5, 5] = 1000f;
		// Walls at columns/rows 3 and 7, interior 4-6.
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
			Assert.That( result[4, 4], Is.EqualTo( 1000f / 9f ).Within( 1f ) );
			Assert.That( result[2, 2], Is.Zero );
		}
	}

	[Test]
	public void Update_TwoLeaves_FlowsAcrossSeamAndConserves() {
		RaggedArrayGrid<float> left = new RaggedArrayGrid<float>( 0, 0, 5, 5 );
		RaggedArrayGrid<float> right = new RaggedArrayGrid<float>( 5, 0, 5, 5 );
		IMutableGrid<float> cells = left;
		cells[4, 2] = 1000f;
		GridTopology<float> topology = Build( new OpenFloatConnectivityStrategy(), left, right );
		Assume.That( topology.Seams.Length, Is.GreaterThan( 0 ) );

		Field<float> source = _compiler.Compile<float, Identity>( topology, default );
		Field<float> destination = new Field<float>( topology );
		for( int i = 0; i < 100; i++ ) {
			_diffusion.Update( source, destination );
			( source, destination ) = ( destination, source );
		}
		_compiler.Decompile<float, Identity>( source, default );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( Sum( left ) + Sum( right ), Is.EqualTo( 1000f ).Within( 0.05f ) );
			Assert.That( Sum( right ), Is.GreaterThan( 100f ) );
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
			_diffusion.Update( source, destination );
			( source, destination ) = ( destination, source );
		}
		_compiler.Decompile<float, Identity>( source, default );
		return grid;
	}

	private static GridTopology<float> Build(
		IConnectivityStrategy<float> strategy,
		params RaggedArrayGrid<float>[] grids
	) {
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		foreach( RaggedArrayGrid<float> grid in grids ) {
			IGrid<float> bounds = grid;
			Assert.That( connectivity.TryAttach( grid, bounds.Column, bounds.Row ), Is.True );
		}
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

	private sealed class Settings : IGridDiffusionSettings {
		public Settings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

}
