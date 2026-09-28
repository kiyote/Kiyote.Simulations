using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Vectorized.Projection.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridProjectionTests {

	private const int Size = 10;

	private readonly IGridProjection _projection;

	public GridProjectionTests() {
		_projection = new GridProjection( new Settings( 20 ) );
	}

	[Test]
	public void Update_ZeroVelocity_RemainsZero() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );

		fields.Step( _projection, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.X.Values.ToArray(), Is.All.Zero );
			Assert.That( fields.Y.Values.ToArray(), Is.All.Zero );
			Assert.That( fields.Pressure.Values.ToArray(), Is.All.Zero );
		}
	}

	[TestCase( true )]
	[TestCase( false )]
	public void Update_MatchesNonVectorizedProjection( bool open ) {
		IConnectivityStrategy<float> strategy = open
			? new OpenFloatConnectivityStrategy()
			: new BoundaryFloatConnectivityStrategy( 0, 0, Size, Size );

		// Reference: the existing non-vectorized projection.
		RaggedArrayGrid<float> pressureIn = new RaggedArrayGrid<float>( Size, Size );
		RaggedArrayGrid<float> pressureOut = new RaggedArrayGrid<float>( Size, Size );
		RaggedArrayGrid<float> divergence = new RaggedArrayGrid<float>( Size, Size );
		RaggedArrayGrid<Velocity> velocityIn = new RaggedArrayGrid<Velocity>( Size, Size );
		RaggedArrayGrid<Velocity> velocityOut = new RaggedArrayGrid<Velocity>( Size, Size );
		IMutableGrid<Velocity> seed = velocityIn;
		seed[4, 5] = new Velocity( -5f, 1f );
		seed[6, 5] = new Velocity( 5f, -2f );
		seed[8, 1] = new Velocity( 3f, 3f );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( pressureIn, 0, 0 );
		connectivity.UpdateConnectivity( strategy );
		Kiyote.Simulations.Projection.IGridProjection reference = new Kiyote.Simulations.Projection.GridProjection();
		Kiyote.Simulations.Projection.FloatProjectionStrategy referenceStrategy = new Kiyote.Simulations.Projection.FloatProjectionStrategy();

		Fields fields = new Fields( connectivity.BuildTopology() );
		fields.X.Values[Index( 4, 5 )] = -5f;
		fields.Y.Values[Index( 4, 5 )] = 1f;
		fields.X.Values[Index( 6, 5 )] = 5f;
		fields.Y.Values[Index( 6, 5 )] = -2f;
		fields.X.Values[Index( 8, 1 )] = 3f;
		fields.Y.Values[Index( 8, 1 )] = 3f;

		const int steps = 3;
		IMutableGrid<float> pIn = pressureIn;
		IMutableGrid<float> pOut = pressureOut;
		IMutableGrid<Velocity> vIn = velocityIn;
		IMutableGrid<Velocity> vOut = velocityOut;
		for( int i = 0; i < steps; i++ ) {
			reference.Update( connectivity, vIn, vOut, pIn, pOut, divergence, referenceStrategy );
			( vIn, vOut ) = ( vOut, vIn );
		}
		fields.Step( _projection, steps );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					Velocity expected = vIn[column, row];
					Assert.That( fields.X.Values[Index( column, row )], Is.EqualTo( expected.X ).Within( 1e-3f ), $"X ({column},{row})" );
					Assert.That( fields.Y.Values[Index( column, row )], Is.EqualTo( expected.Y ).Within( 1e-3f ), $"Y ({column},{row})" );
				}
			}
		}
	}

	[Test]
	public void Update_RepeatedCalls_VelocityRemainsBounded() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );
		fields.X.Values[Index( 5, 5 )] = 5f;
		fields.Y.Values[Index( 8, 2 )] = -10f;

		fields.Step( _projection, 200 );

		float max = 0f;
		for( int i = 0; i < Size * Size; i++ ) {
			max = MathF.Max( max, MathF.Abs( fields.X.Values[i] ) );
			max = MathF.Max( max, MathF.Abs( fields.Y.Values[i] ) );
		}
		Assert.That( float.IsFinite( max ) && max <= 10f, Is.True, $"max={max}" );
	}

	[Test]
	public void Update_BoundaryConnectivity_ImpassableCellsUnchanged() {
		Fields fields = Create( new BoundaryFloatConnectivityStrategy( 3, 3, 5, 5 ) );
		fields.X.Values[Index( 5, 5 )] = 5f;
		fields.X.Values[Index( 1, 1 )] = 7f;

		fields.Step( _projection, 5 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.X.Values[Index( 1, 1 )], Is.EqualTo( 7f ) );
			Assert.That( fields.X.Values[Index( 8, 8 )], Is.Zero );
			Assert.That( fields.Pressure.Values[Index( 1, 1 )], Is.Zero );
		}
	}

	private static int Index( int column, int row ) => ( row * Size ) + column;

	private static Fields Create(
		IConnectivityStrategy<float> strategy
	) {
		RaggedArrayGrid<float> grid = new RaggedArrayGrid<float>( Size, Size );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		Assert.That( connectivity.TryAttach( grid, 0, 0 ), Is.True );
		connectivity.UpdateConnectivity( strategy );
		return new Fields( connectivity.BuildTopology() );
	}

	private sealed class Fields {

		public Fields(
			GridTopology<float> topology
		) {
			X = new Field<float>( topology );
			Y = new Field<float>( topology );
			ScratchX = new Field<float>( topology );
			ScratchY = new Field<float>( topology );
			Pressure = new Field<float>( topology );
			PressureScratch = new Field<float>( topology );
		}

		public Field<float> X { get; private set; }
		public Field<float> Y { get; private set; }
		public Field<float> ScratchX { get; private set; }
		public Field<float> ScratchY { get; private set; }
		public Field<float> Pressure { get; }
		public Field<float> PressureScratch { get; }

		public void Step(
			IGridProjection projection,
			int steps
		) {
			for( int i = 0; i < steps; i++ ) {
				projection.Update( X, Y, ScratchX, ScratchY, Pressure, PressureScratch );
				( X, ScratchX ) = ( ScratchX, X );
				( Y, ScratchY ) = ( ScratchY, Y );
			}
		}
	}

	private sealed class Settings : IGridProjectionSettings {
		public Settings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}

}
