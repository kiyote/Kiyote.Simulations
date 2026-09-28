using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Advection;
using Kiyote.Simulations.Airflow;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Visualizer.Airflow;

internal sealed class GridAirflowVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;
	// Mirrors OpenGridProjectionVisualizer: the pressure/projection relaxation loop
	// inside GridAirflow only lets influence propagate a limited number of cells per
	// call, so several physics steps are advanced per rendered frame.
	public const int StepsPerFrame = 4;

	// Each pump tops its cell up to MaximumPressure/MaximumConcentration every frame and
	// always drives its nozzle at Velocity, so it jets into the grid.
	private static readonly (int Column, int Row, float MaximumPressure, float MaximumConcentration, Velocity Velocity)[] _pumps = [
		( 95, 5, 1000f, 1000f, new Velocity( -30, 30 ) ),
		( 50, 50, 1000f, 1000f, new Velocity( 40, 0 ) ),
	];

	private readonly IGridAirflow _airflow;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly OpenFloatConnectivityStrategy _openConnectivityStrategy;
	private readonly BoundaryFloatConnectivityStrategy _boundaryConnectivityStrategy;
	private readonly FloatProjectionStrategy _projectionStrategy;
	private readonly FloatBilinearSampler _sampler;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridAirflowVisualizer(
		IGridAirflow airflow,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_airflow = airflow;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_openConnectivityStrategy = new OpenFloatConnectivityStrategy();
		_boundaryConnectivityStrategy = new BoundaryFloatConnectivityStrategy( 0, 0, Size, Size );
		_projectionStrategy = new FloatProjectionStrategy();
		_sampler = new FloatBilinearSampler();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		Run( outputFolder, "open", _openConnectivityStrategy );
		Run( outputFolder, "boundary", _boundaryConnectivityStrategy );
	}

	private void Run<TConnectivityStrategy>(
		string outputFolder,
		string name,
		TConnectivityStrategy connectivityStrategy
	)
		where TConnectivityStrategy : IConnectivityStrategy<float> {
		string pressureFileName = _fileSystem.Path.Combine( outputFolder, $"airflow_pressure_{name}.gif" );
		string velocityFileName = _fileSystem.Path.Combine( outputFolder, $"airflow_velocity_{name}.gif" );
		string concentrationFileName = _fileSystem.Path.Combine( outputFolder, $"airflow_concentration_{name}.gif" );

		BufferGrid<float> inputPressure = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> outputPressure = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> projectionPressureSource = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> projectionPressureDestination = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> inputConcentration = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> outputConcentration = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> velocityMagnitude = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );

		RaggedArrayGrid<Velocity> inputVelocity = new RaggedArrayGrid<Velocity>( Size, Size );
		RaggedArrayGrid<Velocity> outputVelocity = new RaggedArrayGrid<Velocity>( Size, Size );

		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( inputPressure, 0, 0 );
		connectivity.UpdateConnectivity( connectivityStrategy );

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( pressureFileName, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( velocityFileName, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder concentrationBuilder = _animation.StartAnimation( concentrationFileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				Pump( inputPressure, inputVelocity, inputConcentration );

				_airflow.Update<float, float, float, float, FloatProjectionStrategy, FloatProjectionStrategy, FloatBilinearSampler>(
					connectivity,
					inputPressure,
					outputPressure,
					_projectionStrategy,
					inputVelocity,
					outputVelocity,
					projectionPressureSource,
					projectionPressureDestination,
					_projectionStrategy,
					inputConcentration,
					outputConcentration,
					_sampler
				);

				( inputPressure, outputPressure ) = ( outputPressure, inputPressure );
				( inputVelocity, outputVelocity ) = ( outputVelocity, inputVelocity );
				( inputConcentration, outputConcentration ) = ( outputConcentration, inputConcentration );
			}

			_op.ScaleToRange( inputPressure, _pixels );
			pressureBuilder.AddFrame( _pixels );

			IMutableGrid<Velocity> currentVelocity = inputVelocity;
			IMutableGrid<float> velocityGrid = velocityMagnitude;
			for( int r = 0; r < Size; r++ ) {
				for( int c = 0; c < Size; c++ ) {
					velocityGrid[c, r] = currentVelocity[c, r].Magnitude;
				}
			}
			_op.ScaleToRange( velocityMagnitude, _pixels );
			velocityBuilder.AddFrame( _pixels );

			_op.ScaleToRange( inputConcentration, _pixels );
			concentrationBuilder.AddFrame( _pixels );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
		concentrationBuilder.FinishAnimation();
	}

	// Tops each pump cell up to its maximum (never removing air that has already
	// accumulated above it) and drives the nozzle at the pump's jet velocity.
	private static void Pump(
		IMutableGrid<float> pressure,
		IMutableGrid<Velocity> velocity,
		IMutableGrid<float> concentration
	) {
		foreach( (int column, int row, float maximumPressure, float maximumConcentration, Velocity pumpVelocity) in _pumps ) {
			if( pressure[column, row] < maximumPressure ) {
				pressure[column, row] = maximumPressure;
			}
			velocity[column, row] = pumpVelocity;
			if( concentration[column, row] < maximumConcentration ) {
				concentration[column, row] = maximumConcentration;
			}
		}
	}

}
