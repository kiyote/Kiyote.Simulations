using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.IntegrationTests;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;
using Kiyote.Simulations.Vectorized.Airflow;
using Kiyote.Simulations.Vectorized.Pressure;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Vectorized.Airflow.IntegrationTests;

[TestFixture]
[ExcludeFromCodeCoverage]
public sealed class GridAirflowTests {

	private const int Size = 10;

	private readonly IGridAirflow _airflow;

	public GridAirflowTests() {
		ISimulationClock clock = new SimulationClock();
		_airflow = new GridAirflow(
			new GridPressure( new PressureSettings( 1f ), clock ),
			new GridProjection( new ProjectionSettings( 20 ) ),
			new GridAdvection( clock ),
			clock
		);
	}

	[Test]
	public void Update_Quiescent_RemainsZero() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );

		fields.Step( _airflow, 5 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Pressure.Values.ToArray(), Is.All.Zero );
			Assert.That( fields.VelocityX.Values.ToArray(), Is.All.Zero );
			Assert.That( fields.VelocityY.Values.ToArray(), Is.All.Zero );
			Assert.That( fields.Concentration.Values.ToArray(), Is.All.Zero );
		}
	}

	[Test]
	public void Update_UniformWind_CarriesConcentrationDownstream() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );
		fields.VelocityX.Values.Fill( 5f );
		fields.Concentration.Values[Index( 3, 5 )] = 1000f;

		fields.Step( _airflow, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Concentration.Values[Index( 4, 5 )], Is.GreaterThan( 0f ) );
			Assert.That( fields.Concentration.Values[Index( 3, 5 )], Is.LessThan( 1000f ) );
			Assert.That( fields.Concentration.Values[Index( 2, 5 )], Is.Zero );
		}
	}

	[Test]
	public void Update_PressurePeak_PushesAirOutward() {
		Fields fields = Create( new OpenFloatConnectivityStrategy() );
		fields.Pressure.Values[Index( 5, 5 )] = 1000f;

		fields.Step( _airflow, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.VelocityX.Values[Index( 6, 5 )], Is.GreaterThan( 0f ) );
			Assert.That( fields.VelocityX.Values[Index( 4, 5 )], Is.LessThan( 0f ) );
		}
	}

	[Test]
	public void Update_RepeatedCalls_RemainsFinite() {
		Fields fields = Create( new BoundaryFloatConnectivityStrategy( 0, 0, Size, Size ) );

		for( int i = 0; i < 200; i++ ) {
			fields.Pressure.Values[Index( 5, 5 )] = 1000f;
			fields.VelocityX.Values[Index( 5, 5 )] = 40f;
			fields.Concentration.Values[Index( 5, 5 )] = 1000f;
			fields.Step( _airflow, 1 );
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Pressure.Values.ToArray(), Has.All.Matches<float>( float.IsFinite ) );
			Assert.That( fields.VelocityX.Values.ToArray(), Has.All.Matches<float>( float.IsFinite ) );
			Assert.That( fields.VelocityY.Values.ToArray(), Has.All.Matches<float>( float.IsFinite ) );
			Assert.That( fields.Concentration.Values.ToArray(), Has.All.Matches<float>( float.IsFinite ) );
		}
	}

	[Test]
	public void Update_BoundaryConnectivity_WallCellsUnchanged() {
		Fields fields = Create( new BoundaryFloatConnectivityStrategy( 3, 3, 5, 5 ) );
		fields.Concentration.Values[Index( 1, 1 )] = 7f;
		fields.Pressure.Values[Index( 5, 5 )] = 1000f;
		fields.Concentration.Values[Index( 5, 5 )] = 1000f;

		fields.Step( _airflow, 10 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( fields.Concentration.Values[Index( 1, 1 )], Is.EqualTo( 7f ) );
			Assert.That( fields.Concentration.Values[Index( 9, 9 )], Is.Zero );
			Assert.That( fields.VelocityX.Values[Index( 1, 1 )], Is.Zero );
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
			Neighbourhood = new AdvectionNeighbourhood<float>( topology );
			ProjectionNeighbourhood = new ProjectionNeighbourhood<float>( topology );
			Pressure = new Field<float>( topology );
			PressureScratch = new Field<float>( topology );
			VelocityX = new Field<float>( topology );
			VelocityY = new Field<float>( topology );
			VelocityScratchX = new Field<float>( topology );
			VelocityScratchY = new Field<float>( topology );
			IntermediateX = new Field<float>( topology );
			IntermediateY = new Field<float>( topology );
			ProjectionPressure = new Field<float>( topology );
			ProjectionPressureScratch = new Field<float>( topology );
			ProjectionDivergence = new Field<float>( topology );
			Concentration = new Field<float>( topology );
			ConcentrationScratch = new Field<float>( topology );
		}

		public AdvectionNeighbourhood<float> Neighbourhood { get; }
		public ProjectionNeighbourhood<float> ProjectionNeighbourhood { get; }
		public Field<float> Pressure { get; private set; }
		public Field<float> PressureScratch { get; private set; }
		public Field<float> VelocityX { get; private set; }
		public Field<float> VelocityY { get; private set; }
		public Field<float> VelocityScratchX { get; private set; }
		public Field<float> VelocityScratchY { get; private set; }
		public Field<float> IntermediateX { get; }
		public Field<float> IntermediateY { get; }
		public Field<float> ProjectionPressure { get; }
		public Field<float> ProjectionPressureScratch { get; }
		public Field<float> ProjectionDivergence { get; }
		public Field<float> Concentration { get; private set; }
		public Field<float> ConcentrationScratch { get; private set; }

		public void Step(
			IGridAirflow airflow,
			int steps
		) {
			for( int i = 0; i < steps; i++ ) {
				airflow.Update(
					Neighbourhood,
					ProjectionNeighbourhood,
					Pressure, PressureScratch,
					VelocityX, VelocityY,
					VelocityScratchX, VelocityScratchY,
					IntermediateX, IntermediateY,
					ProjectionPressure, ProjectionPressureScratch, ProjectionDivergence,
					Concentration, ConcentrationScratch
				);
				( Pressure, PressureScratch ) = ( PressureScratch, Pressure );
				( VelocityX, VelocityScratchX ) = ( VelocityScratchX, VelocityX );
				( VelocityY, VelocityScratchY ) = ( VelocityScratchY, VelocityY );
				( Concentration, ConcentrationScratch ) = ( ConcentrationScratch, Concentration );
			}
		}
	}

	private sealed class PressureSettings : IGridPressureSettings {
		public PressureSettings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}

	private sealed class ProjectionSettings : IGridProjectionSettings {
		public ProjectionSettings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}

}
