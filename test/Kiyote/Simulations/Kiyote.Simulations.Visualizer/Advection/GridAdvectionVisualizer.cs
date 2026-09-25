using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Advection;

namespace Kiyote.Simulations.Visualizer.Advection;

internal sealed class GridAdvectionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridAdvection _advection;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly OpenFloatConnectivityStrategy _connectivityStrategy;
	private readonly FloatBilinearSampler _sampler;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridAdvectionVisualizer(
		IGridAdvection advection,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_advection = advection;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_connectivityStrategy = new OpenFloatConnectivityStrategy();
		_sampler = new FloatBilinearSampler();
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_bufferFactory = bufferFactory;
	}

	public void Execute(
		string outputFolder
	) {
		string concentrationFileName = _fileSystem.Path.Combine( outputFolder, "advection.gif" );
		BufferGrid<float> inputConcentration = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		BufferGrid<float> outputConcentration = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		ArrayGrid<Velocity> velocity = new ArrayGrid<Velocity>( Size, Size );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( inputConcentration, 0, 0 );
		connectivity.UpdateConnectivity( _connectivityStrategy );

		// A uniform diagonal wind carries the seeded plumes across the grid so the
		// animation visibly shows concentration drifting rather than sitting still.
		IMutableGrid<Velocity> velocityGrid = velocity;
		for( int r = 0; r < Size; r++ ) {
			for( int c = 0; c < Size; c++ ) {
				velocityGrid[c, r] = new Velocity( 5, 2 );
			}
		}

		IMutableGrid<float> concentration = inputConcentration;
		concentration[10, 10] = 1000f;
		concentration[10, 80] = 1000f;

		using IAnimationBuilder concentrationBuilder = _animation.StartAnimation( concentrationFileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_advection.Update( connectivity, velocity, inputConcentration, outputConcentration, _sampler );

			_op.ScaleToRange( outputConcentration, _pixels );
			concentrationBuilder.AddFrame( _pixels );
			(inputConcentration, outputConcentration) = (outputConcentration, inputConcentration);
		}
		concentrationBuilder.FinishAnimation();
	}

}
