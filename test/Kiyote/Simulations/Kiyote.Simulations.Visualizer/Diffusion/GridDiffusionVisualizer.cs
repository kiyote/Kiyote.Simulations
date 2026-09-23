using System.IO.Abstractions;
using Kiyote.Buffers;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Visualizer.Diffusion;

internal sealed class GridDiffusionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridDiffusion _diffusion;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<double> _connectivity;
	private readonly DefaultFlowStrategy _flow;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridDiffusionVisualizer(
		IGridDiffusion diffusion,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_diffusion = diffusion;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_connectivity = new AlwaysConnectedStrategy();
		_flow = new DefaultFlowStrategy();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		string fileName = _fileSystem.Path.Combine( outputFolder, "diffusion.gif" );
		BufferGrid<double> input = new BufferGrid<double>( _bufferFactory.Create<double>( Size, Size, 0 ) );
		BufferGrid<double> output = new BufferGrid<double>( _bufferFactory.Create<double>( Size, Size, 0 ) );
		IConnectivityGrid<double> connectivity = new ConnectivityGrid<double>();
		connectivity.TryAttach( input, 0, 0 );
		connectivity.UpdateConnectivity( _connectivity );

		IMutableGrid<double> mutableInput = input;
		mutableInput[90, 10] = 1000d;
		mutableInput[50, 50] = 1000d;

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for (int frame = 0; frame < TotalFrameCount; frame++) {
			_diffusion.Update<double, double, DefaultFlowStrategy>( input, connectivity, output, _flow );
			_op.ScaleToRange( output, _pixels );
			builder.AddFrame( _pixels );
			(input, output) = (output, input);

			
		}
		builder.FinishAnimation();
	}
}
