using Kiyote.Buffers.Numerics;
using Kiyote.Imaging;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.Grids.GridDiffusion;

using Kiyote.Simulations.Visualizer;

internal sealed class GridDiffusionVisualizer {

	public const int Size = 100;

	private readonly IAnimationWriter _animWriter;
	private readonly IGridDiffusion _gridDiffusion;
	private readonly INumericBufferFactory _bufferFactory;
	private readonly INumericBufferOperator _bufferOperation;

	public GridDiffusionVisualizer(
		IAnimationWriter animWriter,
		IGridDiffusion gridDiffusion,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator bufferOperator
	) {
		_animWriter = animWriter;
		_gridDiffusion = gridDiffusion;
		_bufferFactory = bufferFactory;
		_bufferOperation = bufferOperator;
	}

	public void Execute(
		string outputFolder
	) {
		INumericBuffer<byte> stretched = _bufferFactory.Create<byte>( Size, Size, 0 );
		INumericBuffer<double> buffer = _bufferFactory.Create<double>( Size, Size, 0 );
		BufferGrid<double> grid = new BufferGrid<double>( buffer );
		buffer[10, 10] = 10000;
		buffer[90, 50] = 10000;
		buffer[50, 90] = 10000;

		DiffusionStrategy flow = new DiffusionStrategy( 1.0 );
		AlwaysPassableStrategy passability = new AlwaysPassableStrategy();
		BufferSetCellStrategy callback = new BufferSetCellStrategy( buffer );

		IAnimationBuilder builder = _animWriter.StartAnimation( Path.Combine( outputFolder, "griddiffusion.gif" ), TimeSpan.FromMilliseconds( 100 ), 0 );
		_bufferOperation.ScaleToRange( buffer, stretched );
		builder.AddFrame( stretched );
		for( int i = 0; i < 100; i++ ) {
			_gridDiffusion.Flow<double, double, DiffusionStrategy, AlwaysPassableStrategy, BufferSetCellStrategy>(
				grid,
				flow,
				passability,
				callback
			);

			_bufferOperation.ScaleToRange( buffer, stretched );
			builder.AddFrame( stretched );
		}
		builder.FinishAnimation();
	}
}
