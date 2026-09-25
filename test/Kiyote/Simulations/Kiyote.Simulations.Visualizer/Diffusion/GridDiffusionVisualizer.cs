using System.IO.Abstractions;
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
	private readonly IConnectivityStrategy<float> _connectivity;
	private readonly FloatDiffusionStrategy _diffusionStrategy;
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
		_connectivity = new OpenFloatConnectivityStrategy();
		_diffusionStrategy = new FloatDiffusionStrategy();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		string fileName = _fileSystem.Path.Combine( outputFolder, "diffusion.gif" );
		BufferGrid<float> input = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> output = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( input, 0, 0 );
		connectivity.UpdateConnectivity( _connectivity );

		IMutableGrid<float> mutableInput = input;
		mutableInput[90, 10] = 1000f;
		mutableInput[50, 50] = 1000f;

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for (int frame = 0; frame < TotalFrameCount; frame++) {
			_diffusion.Update<float, float, float, FloatDiffusionStrategy>( connectivity, input, output, _diffusionStrategy );
			_op.ScaleToRange( output, _pixels );
			builder.AddFrame( _pixels );
			(input, output) = (output, input);

			
		}
		builder.FinishAnimation();
	}
}
