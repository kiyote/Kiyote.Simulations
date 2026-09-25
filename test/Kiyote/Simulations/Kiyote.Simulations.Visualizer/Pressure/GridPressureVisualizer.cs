using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Pressure;

namespace Kiyote.Simulations.Visualizer.Pressure;

internal sealed class GridPressureVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridPressure _pressure;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _connectivity;
	private readonly FloatPressureStrategy _pressureStrategy;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridPressureVisualizer(
		IGridPressure pressure,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_pressure = pressure;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_connectivity = new OpenFloatConnectivityStrategy();
		_pressureStrategy = new FloatPressureStrategy();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		string fileName = _fileSystem.Path.Combine( outputFolder, "pressure.gif" );
		BufferGrid<float> input = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> output = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( input, 0, 0 );
		connectivity.UpdateConnectivity( _connectivity );

		IMutableGrid<float> mutableInput = input;
		mutableInput[90, 10] = 1000f;
		mutableInput[50, 50] = 1000f;

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_pressure.Update<float, float, float, FloatPressureStrategy>( connectivity, input, output, _pressureStrategy );
			_op.ScaleToRange( output, _pixels );
			builder.AddFrame( _pixels );
			(input, output) = (output, input);
		}
		builder.FinishAnimation();
	}
}
