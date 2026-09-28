using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;
using Kiyote.Simulations.Vectorized.Airflow;
using Kiyote.Simulations.Vectorized.Pressure;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Visualizer.Vectorized.Airflow;

internal sealed class GridAirflowVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;
	public const int StepsPerFrame = 4;

	// Same pumps as the non-vectorized visualizer.
	private static readonly (int Column, int Row, float MaximumPressure, float MaximumConcentration, float VelocityX, float VelocityY)[] _pumps = [
		( 95, 5, 1000f, 1000f, -30f, 30f ),
		( 50, 50, 1000f, 1000f, 40f, 0f ),
	];

	private readonly IGridAirflow _airflow;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _openConnectivity;
	private readonly IConnectivityStrategy<float> _boundaryConnectivity;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

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
		_openConnectivity = new OpenFloatConnectivityStrategy();
		_boundaryConnectivity = new BoundaryFloatConnectivityStrategy( 0, 0, Size, Size );
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		Run( outputFolder, "open", _openConnectivity );
		Run( outputFolder, "boundary", _boundaryConnectivity );
	}

	private void Run(
		string outputFolder,
		string name,
		IConnectivityStrategy<float> connectivityStrategy
	) {
		BufferGrid<float> image = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( image, 0, 0 );
		connectivity.UpdateConnectivity( connectivityStrategy );
		GridTopology<float> topology = connectivity.BuildTopology();

		AdvectionNeighbourhood<float> neighbourhood = new AdvectionNeighbourhood<float>( topology );
		ProjectionNeighbourhood<float> projectionNeighbourhood = new ProjectionNeighbourhood<float>( topology );
		Field<float> inputPressure = new Field<float>( topology );
		Field<float> outputPressure = new Field<float>( topology );
		Field<float> inputVelocityX = new Field<float>( topology );
		Field<float> inputVelocityY = new Field<float>( topology );
		Field<float> outputVelocityX = new Field<float>( topology );
		Field<float> outputVelocityY = new Field<float>( topology );
		Field<float> intermediateVelocityX = new Field<float>( topology );
		Field<float> intermediateVelocityY = new Field<float>( topology );
		Field<float> projectionPressure = new Field<float>( topology );
		Field<float> projectionPressureScratch = new Field<float>( topology );
		Field<float> projectionDivergence = new Field<float>( topology );
		Field<float> inputConcentration = new Field<float>( topology );
		Field<float> outputConcentration = new Field<float>( topology );
		Field<float> velocityMagnitude = new Field<float>( topology );

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"vectorized_airflow_pressure_{name}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"vectorized_airflow_velocity_{name}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder concentrationBuilder = _animation.StartAnimation( _fileSystem.Path.Combine( outputFolder, $"vectorized_airflow_concentration_{name}.gif" ), TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				Pump( inputPressure, inputVelocityX, inputVelocityY, inputConcentration );

				_airflow.Update(
					neighbourhood,
					projectionNeighbourhood,
					inputPressure, outputPressure,
					inputVelocityX, inputVelocityY,
					outputVelocityX, outputVelocityY,
					intermediateVelocityX, intermediateVelocityY,
					projectionPressure, projectionPressureScratch, projectionDivergence,
					inputConcentration, outputConcentration
				);

				(inputPressure, outputPressure) = (outputPressure, inputPressure);
				(inputVelocityX, outputVelocityX) = (outputVelocityX, inputVelocityX);
				(inputVelocityY, outputVelocityY) = (outputVelocityY, inputVelocityY);
				(inputConcentration, outputConcentration) = (outputConcentration, inputConcentration);
			}

			AddFrame( image, inputPressure, pressureBuilder );

			Span<float> magnitude = velocityMagnitude.Values;
			ReadOnlySpan<float> vx = inputVelocityX.Values;
			ReadOnlySpan<float> vy = inputVelocityY.Values;
			for( int i = 0; i < magnitude.Length; i++ ) {
				magnitude[i] = MathF.Sqrt( ( vx[i] * vx[i] ) + ( vy[i] * vy[i] ) );
			}
			AddFrame( image, velocityMagnitude, velocityBuilder );

			AddFrame( image, inputConcentration, concentrationBuilder );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
		concentrationBuilder.FinishAnimation();
	}

	// Single leaf, so field indices map directly to row-major grid positions.
	private void AddFrame(
		BufferGrid<float> image,
		Field<float> field,
		IAnimationBuilder builder
	) {
		IMutableGrid<float> grid = image;
		ReadOnlySpan<float> values = field.Values;
		for( int r = 0; r < Size; r++ ) {
			for( int c = 0; c < Size; c++ ) {
				grid[c, r] = values[( r * Size ) + c];
			}
		}
		_op.ScaleToRange( image, _pixels );
		builder.AddFrame( _pixels );
	}

	private static void Pump(
		Field<float> pressure,
		Field<float> velocityX,
		Field<float> velocityY,
		Field<float> concentration
	) {
		foreach( (int column, int row, float maximumPressure, float maximumConcentration, float x, float y) in _pumps ) {
			int index = ( row * Size ) + column;
			if( pressure.Values[index] < maximumPressure ) {
				pressure.Values[index] = maximumPressure;
			}
			velocityX.Values[index] = x;
			velocityY.Values[index] = y;
			if( concentration.Values[index] < maximumConcentration ) {
				concentration.Values[index] = maximumConcentration;
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
