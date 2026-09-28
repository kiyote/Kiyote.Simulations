using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Projection;

namespace Kiyote.Simulations.Visualizer.Vectorized.Projection;

internal sealed class GridProjectionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;
	public const int StepsPerFrame = 4;

	private readonly IGridProjection _projection;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _openConnectivity;
	private readonly IConnectivityStrategy<float> _boundaryConnectivity;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridProjectionVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_projection = new GridProjection( new ProjectionSettings( 20 ) );
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
		string suffix,
		IConnectivityStrategy<float> connectivityStrategy
	) {
		string pressureFileName = _fileSystem.Path.Combine( outputFolder, $"vectorized_projection_{suffix}.gif" );
		string velocityFileName = _fileSystem.Path.Combine( outputFolder, $"vectorized_velocity_{suffix}.gif" );

		BufferGrid<float> pressureGrid = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> magnitudeGrid = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IMutableGrid<float> pressureCells = pressureGrid;
		IMutableGrid<float> magnitudeCells = magnitudeGrid;
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( pressureGrid, 0, 0 );
		connectivity.UpdateConnectivity( connectivityStrategy );

		// Single leaf at offset 0, so field index = row * Size + column.
		GridTopology<float> topology = connectivity.BuildTopology();
		ProjectionNeighbourhood<float> neighbourhood = new ProjectionNeighbourhood<float>( topology );
		Field<float> inputX = new Field<float>( topology );
		Field<float> inputY = new Field<float>( topology );
		Field<float> outputX = new Field<float>( topology );
		Field<float> outputY = new Field<float>( topology );
		Field<float> pressure = new Field<float>( topology );
		Field<float> pressureScratch = new Field<float>( topology );
		Field<float> divergence = new Field<float>( topology );

		inputX.Values[( 5 * Size ) + 95] = -10f;
		inputY.Values[( 5 * Size ) + 95] = -10f;
		inputX.Values[( 50 * Size ) + 50] = 5f;

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( pressureFileName, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( velocityFileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				_projection.Update( neighbourhood, inputX, inputY, outputX, outputY, pressure, pressureScratch, divergence );
				(inputX, outputX) = (outputX, inputX);
				(inputY, outputY) = (outputY, inputY);
			}

			for( int r = 0; r < Size; r++ ) {
				for( int c = 0; c < Size; c++ ) {
					int index = ( r * Size ) + c;
					pressureCells[c, r] = pressure.Values[index];
					float x = inputX.Values[index];
					float y = inputY.Values[index];
					magnitudeCells[c, r] = MathF.Sqrt( ( x * x ) + ( y * y ) );
				}
			}

			_op.ScaleToRange( pressureGrid, _pixels );
			pressureBuilder.AddFrame( _pixels );
			_op.ScaleToRange( magnitudeGrid, _pixels );
			velocityBuilder.AddFrame( _pixels );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
	}

	private sealed class ProjectionSettings : IGridProjectionSettings {
		public ProjectionSettings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}
}
