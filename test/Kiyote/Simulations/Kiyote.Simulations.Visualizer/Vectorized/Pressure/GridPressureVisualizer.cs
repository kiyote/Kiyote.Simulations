using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Imaging;
using Kiyote.Simulations.Vectorized;
using Kiyote.Simulations.Vectorized.Pressure;

namespace Kiyote.Simulations.Visualizer.Vectorized.Pressure;

internal sealed class GridPressureVisualizer {

	public const int Size = 100;
	public const int TotalFrameCount = 100;

	private readonly IGridPressure _pressure;
	private readonly IFieldCompiler _compiler;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IConnectivityStrategy<float> _openConnectivity;
	private readonly IConnectivityStrategy<float> _boundaryConnectivity;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBufferFactory _bufferFactory;

	public GridPressureVisualizer(
		ISimulationClock clock,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		// Rate * dt = 1/9 per step, matching the spread of the non-vectorized visualizer.
		_pressure = new GridPressure( new PressureSettings( 1f / 9f / clock.FixedTimeStep ), clock );
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
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_pressure_open.gif" ), _openConnectivity );
		Run( _fileSystem.Path.Combine( outputFolder, "vectorized_pressure_boundary.gif" ), _boundaryConnectivity );
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
			_pressure.Update( input, output );
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

	private sealed class PressureSettings : IGridPressureSettings {
		public PressureSettings( float rate ) {
			Rate = rate;
		}
		public float Rate { get; }
	}
}
