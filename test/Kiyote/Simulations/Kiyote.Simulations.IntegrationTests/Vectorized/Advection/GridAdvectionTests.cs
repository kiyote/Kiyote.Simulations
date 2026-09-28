using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;

namespace Kiyote.Simulations.Vectorized.Advection.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridAdvectionTests {

	private const int Size = 10;

	private readonly ISimulationClock _clock;
	private readonly IGridAdvection _advection;

	public GridAdvectionTests() {
		_clock = new SimulationClock();
		_advection = new GridAdvection( _clock );
	}

	[Test]
	public void Update_UniformWind_ShiftsOneCell() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );
		fields.VelocityX.Values.Fill( 10f );
		fields.Source.Values[Index( 3, 5 )] = 1000f;
		fields.Source.Values[Index( 6, 2 )] = 500f;

		_advection.Update( fields.Neighbourhood, fields.VelocityX, fields.VelocityY, fields.Source, fields.Destination );

		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					float expected = ( column, row ) switch {
						(4, 5) => 1000f,
						(7, 2) => 500f,
						_ => 0f
					};
					Assert.That( fields.Destination.Values[Index( column, row )], Is.EqualTo( expected ).Within( 1e-4f ), $"({column},{row})" );
				}
			}
		}
	}

	[Test]
	public void Update_MatchesNonVectorizedAdvection() {
		RaggedArrayGrid<float> referenceSource = new RaggedArrayGrid<float>( Size, Size );
		RaggedArrayGrid<float> referenceDestination = new RaggedArrayGrid<float>( Size, Size );
		RaggedArrayGrid<Velocity> referenceVelocity = new RaggedArrayGrid<Velocity>( Size, Size );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( referenceSource, 0, 0 );
		connectivity.UpdateConnectivity( new OpenFloatConnectivityStrategy() );
		Fields fields = new Fields( connectivity.BuildTopology() );

		IMutableGrid<float> source = referenceSource;
		IMutableGrid<Velocity> velocity = referenceVelocity;
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				float value = ( ( column * 7 ) + ( row * 13 ) ) % 17;
				float x = ( ( ( column * 3 ) + row ) % 19 ) - 9f;
				float y = ( ( ( row * 5 ) + column ) % 19 ) - 9f;
				source[column, row] = value;
				velocity[column, row] = new Velocity( x, y );
				fields.Source.Values[Index( column, row )] = value;
				fields.VelocityX.Values[Index( column, row )] = x;
				fields.VelocityY.Values[Index( column, row )] = y;
			}
		}

		Kiyote.Simulations.Advection.IGridAdvection reference = new Kiyote.Simulations.Advection.GridAdvection( _clock );
		reference.Update( connectivity, referenceVelocity, referenceSource, referenceDestination, new Kiyote.Simulations.Advection.FloatBilinearSampler() );
		_advection.Update( fields.Neighbourhood, fields.VelocityX, fields.VelocityY, fields.Source, fields.Destination );

		IGrid<float> expected = referenceDestination;
		using( Assert.EnterMultipleScope() ) {
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					Assert.That( fields.Destination.Values[Index( column, row )], Is.EqualTo( expected[column, row] ).Within( 1e-4f ), $"({column},{row})" );
				}
			}
		}
	}

	[Test]
	public void Update_BoundaryConnectivity_WallCellsUnchangedAndNotSampled() {
		Fields fields = Create( new BoundaryFloatConnectivityStrategy( 3, 3, 5, 5 ) );
		fields.VelocityX.Values.Fill( 10f );
		fields.Source.Values[Index( 2, 5 )] = 1000f;
		fields.Source.Values[Index( 4, 5 )] = 50f;

		_advection.Update( fields.Neighbourhood, fields.VelocityX, fields.VelocityY, fields.Source, fields.Destination );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Destination.Values[Index( 2, 5 )], Is.EqualTo( 1000f ) );
			Assert.That( fields.Destination.Values[Index( 3, 5 )], Is.Zero );
			Assert.That( fields.Destination.Values[Index( 5, 5 )], Is.EqualTo( 50f ).Within( 1e-4f ) );
		}
	}

	[Test]
	public void Update_AcrossSeam_SamplesNeighbouringLeaf() {
		RaggedArrayGrid<float> left = new RaggedArrayGrid<float>( 5, Size );
		RaggedArrayGrid<float> right = new RaggedArrayGrid<float>( 5, Size );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		Assert.That( connectivity.TryAttach( left, 0, 0 ), Is.True );
		Assert.That( connectivity.TryAttach( right, 5, 0 ), Is.True );
		connectivity.UpdateConnectivity( new OpenFloatConnectivityStrategy() );
		GridTopology<float> topology = connectivity.BuildTopology();
		Fields fields = new Fields( topology );

		int leftEdge = LeafIndex( topology, 4, 5 );
		int rightEdge = LeafIndex( topology, 5, 5 );
		fields.VelocityX.Values.Fill( 10f );
		fields.Source.Values[leftEdge] = 1000f;

		_advection.Update( fields.Neighbourhood, fields.VelocityX, fields.VelocityY, fields.Source, fields.Destination );

		Assert.That( fields.Destination.Values[rightEdge], Is.EqualTo( 1000f ).Within( 1e-4f ) );
	}

	[Test]
	public void Update_MultiCellDisplacement_CarriedFullDistance() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );
		fields.VelocityX.Values.Fill( 20f );
		fields.Source.Values[Index( 3, 5 )] = 1000f;

		_advection.Update( fields.Neighbourhood, fields.VelocityX, fields.VelocityY, fields.Source, fields.Destination );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Destination.Values[Index( 5, 5 )], Is.EqualTo( 1000f ).Within( 1e-4f ) );
			Assert.That( fields.Destination.Values[Index( 4, 5 )], Is.Zero );
		}
	}

	private static int Index( int column, int row ) => ( row * Size ) + column;

	private static int LeafIndex(
		GridTopology<float> topology,
		int column,
		int row
	) {
		foreach( TopologyLeaf<float> leaf in topology.Leaves ) {
			if( column >= leaf.Column && column < leaf.Column + leaf.Width
				&& row >= leaf.Row && row < leaf.Row + leaf.Height
			) {
				return leaf.Offset + ( ( row - leaf.Row ) * leaf.Width ) + ( column - leaf.Column );
			}
		}
		throw new ArgumentOutOfRangeException( nameof( column ) );
	}

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
			Neighbourhood = new AdvectionNeighbourhood<float>( topology );
			VelocityX = new Field<float>( topology );
			VelocityY = new Field<float>( topology );
			Source = new Field<float>( topology );
			Destination = new Field<float>( topology );
		}

		public AdvectionNeighbourhood<float> Neighbourhood { get; }
		public Field<float> VelocityX { get; }
		public Field<float> VelocityY { get; }
		public Field<float> Source { get; }
		public Field<float> Destination { get; }
	}

}
