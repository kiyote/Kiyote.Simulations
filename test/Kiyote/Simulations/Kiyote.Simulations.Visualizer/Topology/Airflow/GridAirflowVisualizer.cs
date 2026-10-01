using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Airflow;
using Kiyote.Simulations.Topology.Pressure;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Visualizer.Topology.Airflow;

internal sealed class GridAirflowVisualizer {

	public const int Size = 100;
	public const int ChunkSize = 16;
	public const int TotalFrameCount = 100;
	public const int StepsPerFrame = 4;

	// Same pumps as the vectorized visualizer.
	private static readonly (int Column, int Row, float MaximumPressure, float MaximumConcentration, float VelocityX, float VelocityY)[] _pumps = [
		( 95, 5, 1000f, 1000f, -30f, 30f ),
		( 50, 50, 1000f, 1000f, 40f, 0f ),
	];

	private readonly IGridAirflow _airflow;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _values;

	public GridAirflowVisualizer(
		ISimulationClock clock,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		// Rate * dt = 1/9 per step, matching the spread of the non-vectorized pressure.
		_airflow = new GridAirflow(
			new GridPressure( new PressureSettings( 1f / 9f / clock.FixedTimeStep ), clock ),
			new GridProjection( new ProjectionSettings( 20 ) ),
			new GridAdvection( clock ),
			clock
		);
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_values = bufferFactory.Create<float>( Size, Size, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		Run( outputFolder, "open", new OpenConnectivity() );
		Run( outputFolder, "boundary", new BoundaryConnectivity( 0, 0, Size, Size ) );
	}

	private void Run<TStrategy>(
		string outputFolder,
		string suffix,
		TStrategy connectivityStrategy
	) where TStrategy : struct, IConnectivityStrategy<float> {
		DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, 0f );
			}
		}
		IGridAssembly<float> assembly = new GridAssembly<float>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, ChunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( compiled, connectivityStrategy, 0 );
		AirflowNeighbourhood neighbourhood = _airflow.CreateNeighbourhood( compiled, connectivity );
		IGridLayer<float> inputPressure = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputPressure = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> inputVelocityX = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> inputVelocityY = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputVelocityX = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputVelocityY = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> intermediateVelocityX = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> intermediateVelocityY = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> projectionPressure = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> projectionPressureScratch = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> projectionDivergence = compiled.CreateLayer<float>( 0 );
		IGridLayer<float> inputConcentration = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputConcentration = compiled.CreateLayer<float>( 1 );

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"topology_airflow_pressure_{suffix}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"topology_airflow_velocity_{suffix}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder concentrationBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"topology_airflow_concentration_{suffix}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				Pump( inputPressure, inputVelocityX, inputVelocityY, inputConcentration );

				_airflow.Update(
					neighbourhood,
					inputPressure, outputPressure,
					inputVelocityX, inputVelocityY,
					outputVelocityX, outputVelocityY,
					intermediateVelocityX, intermediateVelocityY,
					projectionPressure, projectionPressureScratch, projectionDivergence,
					inputConcentration, outputConcentration
				);

				compiled.Swap( inputPressure, outputPressure );
				compiled.Swap( inputVelocityX, outputVelocityX );
				compiled.Swap( inputVelocityY, outputVelocityY );
				compiled.Swap( inputConcentration, outputConcentration );
			}

			AddFrame( inputPressure, pressureBuilder );

			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					float x = inputVelocityX[column, row];
					float y = inputVelocityY[column, row];
					_values[column, row] = MathF.Sqrt( ( x * x ) + ( y * y ) );
				}
			}
			_op.ScaleToRange( _values, _pixels );
			velocityBuilder.AddFrame( _pixels );

			AddFrame( inputConcentration, concentrationBuilder );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
		concentrationBuilder.FinishAnimation();
	}

	private void AddFrame(
		IGridLayer<float> layer,
		IAnimationBuilder builder
	) {
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				_values[column, row] = layer[column, row];
			}
		}
		_op.ScaleToRange( _values, _pixels );
		builder.AddFrame( _pixels );
	}

	private static void Pump(
		IGridLayer<float> pressure,
		IGridLayer<float> velocityX,
		IGridLayer<float> velocityY,
		IGridLayer<float> concentration
	) {
		foreach( (int column, int row, float maximumPressure, float maximumConcentration, float x, float y) in _pumps ) {
			if( pressure[column, row] < maximumPressure ) {
				pressure[column, row] = maximumPressure;
			}
			velocityX[column, row] = x;
			velocityY[column, row] = y;
			if( concentration[column, row] < maximumConcentration ) {
				concentration[column, row] = maximumConcentration;
			}
		}
	}

	private readonly struct OpenConnectivity : IConnectivityStrategy<float> {
		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied;
	}

	private readonly struct BoundaryConnectivity : IConnectivityStrategy<float> {
		private readonly int _left;
		private readonly int _top;
		private readonly int _width;
		private readonly int _height;

		public BoundaryConnectivity(
			int left,
			int top,
			int width,
			int height
		) {
			_left = left;
			_top = top;
			_width = width;
			_height = height;
		}

		bool IConnectivityStrategy<float>.Evaluate(
			in TopologyCell<float> source,
			in TopologyCell<float> destination,
			Direction direction,
			in TopologyCell<float> orthogonalA,
			in TopologyCell<float> orthogonalB,
			bool isSeam
		) => destination.IsOccupied && IsPassable( source ) && IsPassable( destination );

		// The outermost ring of the rectangle (all four sides) is the wall.
		private bool IsPassable(
			in TopologyCell<float> cell
		) => cell.Column > _left
			&& cell.Column < _left + _width - 1
			&& cell.Row > _top
			&& cell.Row < _top + _height - 1;
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
