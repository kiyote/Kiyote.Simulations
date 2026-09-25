using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Projection;

namespace Kiyote.Simulations.Visualizer.Projection;

internal sealed class BoundaryGridProjectionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;
	// The Jacobi relaxation loop inside GridProjection.Update only lets pressure/
	// velocity influence propagate a limited number of cells per call, so several
	// physics steps are advanced per rendered frame to make motion visibly faster
	// without changing the underlying solver's convergence behavior.
	public const int StepsPerFrame = 4;

	private readonly IGridProjection _projection;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly BoundaryFloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatProjectionStrategy _projectionStrategy;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public BoundaryGridProjectionVisualizer(
		IGridProjection projection,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_projection = projection;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_connectivityStrategy = new BoundaryFloatConnectivityStrategy( 0, 0, Size, Size );
		_projectionStrategy = new FloatProjectionStrategy();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		string pressureFileName = _fileSystem.Path.Combine( outputFolder, "projection_boundary.gif" );
		string velocityFileName = _fileSystem.Path.Combine( outputFolder, "velocity_boundary.gif" );
		BufferGrid<float> inputPressure = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> outputPressure = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> velocityMagnitude = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IMutableGrid<float> velocityGrid = velocityMagnitude;
		RaggedArrayGrid<Velocity> inputVelocity = new RaggedArrayGrid<Velocity>( 100, 100 );
		RaggedArrayGrid<Velocity> outputVelocity = new RaggedArrayGrid<Velocity>( 100, 100 );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( inputPressure, 0, 0 );
		connectivity.UpdateConnectivity( _connectivityStrategy );

		IMutableGrid<float> pressure = inputPressure;
		pressure[95, 5] = 1000f;
		pressure[50, 50] = 1000f;

		IMutableGrid<Velocity> velocity = inputVelocity;
		velocity[95, 5] = new Velocity( -10, -10 );
		velocity[50, 50] = new Velocity( 5, 0 );

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( pressureFileName, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( velocityFileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				_projection.Update( connectivity, inputVelocity, outputVelocity, inputPressure, outputPressure, _projectionStrategy );
				(inputPressure, outputPressure) = (outputPressure, inputPressure);
				(inputVelocity, outputVelocity) = (outputVelocity, inputVelocity);
			}

			_op.ScaleToRange( inputPressure, _pixels );
			pressureBuilder.AddFrame( _pixels );

			IMutableGrid<Velocity> outputVelocityGrid = inputVelocity;
			for( int r = 0; r < Size; r++ ) {
				for( int c = 0; c < Size; c++ ) {
					velocityGrid[c, r] = outputVelocityGrid[c, r].Magnitude;
				}
			}
			_op.ScaleToRange( velocityMagnitude, _pixels );
			velocityBuilder.AddFrame( _pixels );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
	}

}
