using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Diffusion;

namespace Kiyote.Simulations.Visualizer.Vectorized.Diffusion;

internal sealed class GridDiffusionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridDiffusion _diffusion;
	private readonly IFieldCompiler _compiler;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _openConnectivity;
	private readonly IConnectivityStrategy<float> _boundaryConnectivity;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridDiffusionVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_diffusion = new GridDiffusion( new DiffusionSettings( 1f / 9f ) );
		_compiler = new FieldCompiler();
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
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_diffusion_open.gif" ), _openConnectivity );
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_diffusion_boundary.gif" ), _boundaryConnectivity );
	}

	private void Run(
		string fileName,
		IConnectivityStrategy<float> connectivityStrategy
	) {
		BufferGrid<float> grid = new BufferGrid<float>( _bufferFactory.Create<float>( Size, Size, 0 ) );
		IConnectivityGrid<float> connectivity = new ConnectivityGrid<float>();
		connectivity.TryAttach( grid, 0, 0 );
		connectivity.UpdateConnectivity( connectivityStrategy );

		IMutableGrid<float> mutableGrid = grid;
		mutableGrid[90, 10] = 1000f;
		mutableGrid[50, 50] = 1000f;

		GridTopology<float> topology = connectivity.BuildTopology();
		Field<float> input = _compiler.Compile<float, IdentitySelector>( topology, default );
		Field<float> output = new Field<float>( topology );

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_diffusion.Update( input, output );
			_compiler.Decompile<float, IdentitySelector>( output, default );
			_op.ScaleToRange( grid, _pixels );
			builder.AddFrame( _pixels );
			(input, output) = (output, input);
		}
		builder.FinishAnimation();
	}

	private readonly struct IdentitySelector : IFieldSelector<float> {
		float IFieldSelector<float>.GetValue( float cell ) => cell;
		float IFieldSelector<float>.SetValue( float cell, float value ) => value;
	}

	private sealed class DiffusionSettings : IGridDiffusionSettings {
		public DiffusionSettings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}
}
