using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Airflow;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Airflow.IntegrationTests;

[TestFixture( 8 )]
[TestFixture( 16 )]
[ExcludeFromCodeCoverage]
public sealed class GridAirflowTests {

	private const int Size = 20;

	private readonly IGridAirflow _airflow;
	private readonly int _chunkSize;

	public GridAirflowTests( int chunkSize ) {
		_chunkSize = chunkSize;
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
		using Layers layers = Layers.Create( _airflow, new Open(), _chunkSize );

		layers.Step( _airflow, 5 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( Layers.MaxAbs( layers.Pressure ), Is.Zero );
			Assert.That( Layers.MaxAbs( layers.VelocityX ), Is.Zero );
			Assert.That( Layers.MaxAbs( layers.VelocityY ), Is.Zero );
			Assert.That( Layers.MaxAbs( layers.Concentration ), Is.Zero );
		}
	}

	[Test]
	public void Update_UniformWind_CarriesConcentrationDownstream() {
		using Layers layers = Layers.Create( _airflow, new Open(), _chunkSize );
		Layers.Fill( layers.VelocityX, 5f );
		layers.Concentration[8, 10] = 1000f;

		layers.Step( _airflow, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( layers.Concentration[9, 10], Is.GreaterThan( 0f ) );
			Assert.That( layers.Concentration[8, 10], Is.LessThan( 1000f ) );
			Assert.That( layers.Concentration[7, 10], Is.Zero );
		}
	}

	[Test]
	public void Update_PressurePeak_PushesAirOutward() {
		using Layers layers = Layers.Create( _airflow, new Open(), _chunkSize );
		layers.Pressure[10, 10] = 1000f;

		layers.Step( _airflow, 1 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( layers.VelocityX[11, 10], Is.GreaterThan( 0f ) );
			Assert.That( layers.VelocityX[9, 10], Is.LessThan( 0f ) );
		}
	}

	[Test]
	public void Update_PressurePeakAcrossChunkBoundary_PushesAirOutward() {
		using Layers layers = Layers.Create( _airflow, new Open(), _chunkSize );
		layers.Pressure[_chunkSize - 1, 5] = 1000f;

		layers.Step( _airflow, 1 );

		Assert.That( layers.VelocityX[_chunkSize, 5], Is.GreaterThan( 0f ) );
	}

	[Test]
	public void Update_RepeatedCalls_RemainsFinite() {
		using Layers layers = Layers.Create( _airflow, new Box( 1, 1, Size - 2, Size - 2 ), _chunkSize );

		for( int i = 0; i < 200; i++ ) {
			layers.Pressure[10, 10] = 1000f;
			layers.VelocityX[10, 10] = 40f;
			layers.Concentration[10, 10] = 1000f;
			layers.Step( _airflow, 1 );
		}

		using( Assert.EnterMultipleScope() ) {
			Assert.That( float.IsFinite( Layers.MaxAbs( layers.Pressure ) ), Is.True );
			Assert.That( float.IsFinite( Layers.MaxAbs( layers.VelocityX ) ), Is.True );
			Assert.That( float.IsFinite( Layers.MaxAbs( layers.VelocityY ) ), Is.True );
			Assert.That( float.IsFinite( Layers.MaxAbs( layers.Concentration ) ), Is.True );
		}
	}

	[Test]
	public void Update_WalledBox_OutsideUndisturbed() {
		using Layers layers = Layers.Create( _airflow, new Box( 6, 6, 14, 14 ), _chunkSize );
		layers.Pressure[10, 10] = 1000f;
		layers.Concentration[10, 10] = 1000f;

		layers.Step( _airflow, 10 );

		using( Assert.EnterMultipleScope() ) {
			Assert.That( layers.Pressure[2, 2], Is.Zero );
			Assert.That( layers.Concentration[2, 2], Is.Zero );
			Assert.That( layers.VelocityX[2, 2], Is.Zero.Within( 1e-4f ) );
		}
	}

	private sealed class Layers : IDisposable {

		private readonly ICompiledGridAssembly<float> _compiled;
		private readonly AirflowNeighbourhood _neighbourhood;
		private readonly IGridLayer<float> _pressureScratch;
		private readonly IGridLayer<float> _velocityScratchX;
		private readonly IGridLayer<float> _velocityScratchY;
		private readonly IGridLayer<float> _intermediateX;
		private readonly IGridLayer<float> _intermediateY;
		private readonly IGridLayer<float> _projectionPressure;
		private readonly IGridLayer<float> _projectionPressureScratch;
		private readonly IGridLayer<float> _projectionDivergence;
		private readonly IGridLayer<float> _concentrationScratch;

		public static Layers Create<TStrategy>(
			IGridAirflow airflow,
			TStrategy strategy,
			int chunkSize
		) where TStrategy : struct, IConnectivityStrategy<float> {
			DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					grid.TrySetCell( column, row, 0f );
				}
			}
			IGridAssembly<float> assembly = new GridAssembly<float>();
			Assert.That( assembly.TryAttach( grid, 0, 0 ).Succeeded, Is.True );
			ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, chunkSize );
			IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( compiled, strategy, 0 );
			return new Layers( compiled, airflow.CreateNeighbourhood( compiled, connectivity ) );
		}

		private Layers(
			ICompiledGridAssembly<float> compiled,
			AirflowNeighbourhood neighbourhood
		) {
			_compiled = compiled;
			_neighbourhood = neighbourhood;
			Pressure = _compiled.CreateLayer<float>( 1 );
			_pressureScratch = _compiled.CreateLayer<float>( 1 );
			VelocityX = _compiled.CreateLayer<float>( 1 );
			VelocityY = _compiled.CreateLayer<float>( 1 );
			_velocityScratchX = _compiled.CreateLayer<float>( 1 );
			_velocityScratchY = _compiled.CreateLayer<float>( 1 );
			_intermediateX = _compiled.CreateLayer<float>( 1 );
			_intermediateY = _compiled.CreateLayer<float>( 1 );
			_projectionPressure = _compiled.CreateLayer<float>( 1 );
			_projectionPressureScratch = _compiled.CreateLayer<float>( 1 );
			_projectionDivergence = _compiled.CreateLayer<float>( 0 );
			Concentration = _compiled.CreateLayer<float>( 1 );
			_concentrationScratch = _compiled.CreateLayer<float>( 1 );
		}

		public IGridLayer<float> Pressure { get; }
		public IGridLayer<float> VelocityX { get; }
		public IGridLayer<float> VelocityY { get; }
		public IGridLayer<float> Concentration { get; }

		public void Step(
			IGridAirflow airflow,
			int steps
		) {
			for( int i = 0; i < steps; i++ ) {
				airflow.Update(
					_neighbourhood,
					Pressure, _pressureScratch,
					VelocityX, VelocityY,
					_velocityScratchX, _velocityScratchY,
					_intermediateX, _intermediateY,
					_projectionPressure, _projectionPressureScratch, _projectionDivergence,
					Concentration, _concentrationScratch
				);
				_compiled.Swap( Pressure, _pressureScratch );
				_compiled.Swap( VelocityX, _velocityScratchX );
				_compiled.Swap( VelocityY, _velocityScratchY );
				_compiled.Swap( Concentration, _concentrationScratch );
			}
		}

		public static void Fill(
			IGridLayer<float> layer,
			float value
		) {
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					layer[column, row] = value;
				}
			}
		}

		public static float MaxAbs(
			IGridLayer<float> layer
		) {
			float max = 0f;
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					float value = layer[column, row];
					max = float.IsFinite( value ) ? MathF.Max( max, MathF.Abs( value ) ) : float.PositiveInfinity;
				}
			}
			return max;
		}

		public void Dispose() {
			_compiled.Dispose();
		}
	}

	private readonly struct Open : IConnectivityStrategy<float> {
		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied;
	}

	private readonly struct Box : IConnectivityStrategy<float> {
		private readonly int _left;
		private readonly int _top;
		private readonly int _right;
		private readonly int _bottom;

		public Box( int left, int top, int right, int bottom ) {
			_left = left;
			_top = top;
			_right = right;
			_bottom = bottom;
		}

		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied && Inside( source ) == Inside( destination );

		private bool Inside( in TopologyCell<float> cell ) =>
			cell.Column >= _left && cell.Column <= _right && cell.Row >= _top && cell.Row <= _bottom;
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
