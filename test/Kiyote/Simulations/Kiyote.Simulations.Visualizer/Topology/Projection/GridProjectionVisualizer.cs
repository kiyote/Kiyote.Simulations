using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Visualizer.Topology.Projection;

internal sealed class GridProjectionVisualizer {

	public const int Size = 100;
	public const int ChunkSize = 16;
	public const int TotalFrameCount = 100;
	public const int StepsPerFrame = 4;

	private readonly IGridProjection _projection;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _pressureValues;
	private readonly INumericBuffer<float> _magnitudeValues;

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
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_pressureValues = bufferFactory.Create<float>( Size, Size, 0 );
		_magnitudeValues = bufferFactory.Create<float>( Size, Size, 0 );
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
		string pressureFileName = _fileSystem.Path.Combine( outputFolder, $"topology_projection_{suffix}.gif" );
		string velocityFileName = _fileSystem.Path.Combine( outputFolder, $"topology_velocity_{suffix}.gif" );

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
		ProjectionNeighbourhood neighbourhood = _projection.CreateNeighbourhood( compiled, connectivity );
		IGridLayer<float> inputX = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> inputY = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputX = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> outputY = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> pressure = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> pressureScratch = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> divergence = compiled.CreateLayer<float>( 0 );

		inputX[95, 5] = -10f;
		inputY[95, 5] = -10f;
		inputX[50, 50] = 5f;

		using IAnimationBuilder pressureBuilder = _animation.StartAnimation( pressureFileName, TimeSpan.FromMilliseconds( 100 ) );
		using IAnimationBuilder velocityBuilder = _animation.StartAnimation( velocityFileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int step = 0; step < StepsPerFrame; step++ ) {
				_projection.Update( neighbourhood, inputX, inputY, outputX, outputY, pressure, pressureScratch, divergence );
				compiled.Swap( inputX, outputX );
				compiled.Swap( inputY, outputY );
			}

			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					_pressureValues[column, row] = pressure[column, row];
					float x = inputX[column, row];
					float y = inputY[column, row];
					_magnitudeValues[column, row] = MathF.Sqrt( ( x * x ) + ( y * y ) );
				}
			}

			_op.ScaleToRange( _pressureValues, _pixels );
			pressureBuilder.AddFrame( _pixels );
			_op.ScaleToRange( _magnitudeValues, _pixels );
			velocityBuilder.AddFrame( _pixels );
		}
		pressureBuilder.FinishAnimation();
		velocityBuilder.FinishAnimation();
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

	private sealed class ProjectionSettings : IGridProjectionSettings {
		public ProjectionSettings( int iterations ) {
			Iterations = iterations;
		}
		public int Iterations { get; }
	}
}
