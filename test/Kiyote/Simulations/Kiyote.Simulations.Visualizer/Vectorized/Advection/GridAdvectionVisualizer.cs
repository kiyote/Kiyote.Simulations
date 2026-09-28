using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Advection;

namespace Kiyote.Simulations.Visualizer.Vectorized.Advection;

internal sealed class GridAdvectionVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridAdvection _advection;
	private readonly IFieldCompiler _compiler;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _openConnectivity;
	private readonly IConnectivityStrategy<float> _boundaryConnectivity;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridAdvectionVisualizer(
		ISimulationClock clock,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_advection = new GridAdvection( clock );
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
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_advection_open.gif" ), _openConnectivity );
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_advection_boundary.gif" ), _boundaryConnectivity );
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
		mutableGrid[10, 10] = 1000f;
		mutableGrid[10, 80] = 1000f;

		GridTopology<float> topology = connectivity.BuildTopology();
		AdvectionNeighbourhood<float> neighbourhood = new AdvectionNeighbourhood<float>( topology );
		Field<float> input = _compiler.Compile<float, IdentitySelector>( topology, default );
		Field<float> output = new Field<float>( topology );
		Field<float> velocityX = new Field<float>( topology );
		Field<float> velocityY = new Field<float>( topology );
		// A uniform diagonal wind, matching the non-vectorized visualizer.
		velocityX.Values.Fill( 5f );
		velocityY.Values.Fill( 2f );

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_advection.Update( neighbourhood, velocityX, velocityY, input, output );
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
}
