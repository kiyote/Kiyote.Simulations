using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.Topology.Advection;

namespace Kiyote.Simulations.Visualizer.Topology.Advection;

internal sealed class GridAdvectionVisualizer {

	public const int Size = 100;
	public const int ChunkSize = 16;
	public const int TotalFrameCount = 100;

	private readonly IGridAdvection _advection;
	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly INumericBufferOperator _op;

	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _values;

	public GridAdvectionVisualizer(
		ISimulationClock clock,
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op
	) {
		_advection = new GridAdvection( clock );
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_pixels = bufferFactory.Create<byte>( Size, Size, 0 );
		_values = bufferFactory.Create<float>( Size, Size, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		Run( _fileSystem.Path.Combine( outputFolder, "topology_advection_open.gif" ), new OpenConnectivity() );
		Run( _fileSystem.Path.Combine( outputFolder, "topology_advection_boundary.gif" ), new BoundaryConnectivity( 0, 0, Size, Size ) );
	}

	private void Run<TStrategy>(
		string fileName,
		TStrategy connectivityStrategy
	) where TStrategy : struct, IConnectivityStrategy<float> {
		DenseGridSource<float> grid = new DenseGridSource<float>( Size, Size );
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				grid.TrySetCell( column, row, 0f );
			}
		}
		grid.GetCell( 10, 10 ) = 1000f;
		grid.GetCell( 10, 80 ) = 1000f;

		IGridAssembly<float> assembly = new GridAssembly<float>();
		if( !assembly.TryAttach( grid, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}
		using ICompiledGridAssembly<float> compiled = new GridCompiler().Compile( assembly, ChunkSize );
		IGridLayer<Direction> connectivity = new ConnectivityBuilder().Build( compiled, connectivityStrategy, 0 );
		AdvectionNeighbourhood neighbourhood = _advection.CreateNeighbourhood( compiled, connectivity );
		IGridLayer<float> values = compiled.Bind<float, IdentityBinding>( default, 1 );
		IGridLayer<float> scratch = compiled.CreateLayer<float>( 1 );
		IGridLayer<float> velocityX = compiled.CreateLayer<float>( 0 );
		IGridLayer<float> velocityY = compiled.CreateLayer<float>( 0 );
		// A uniform diagonal wind, matching the other advection visualizers.
		for( int row = 0; row < Size; row++ ) {
			for( int column = 0; column < Size; column++ ) {
				velocityX[column, row] = 5f;
				velocityY[column, row] = 2f;
			}
		}

		using IAnimationBuilder builder = _animation.StartAnimation( fileName, TimeSpan.FromMilliseconds( 100 ) );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_advection.Update( neighbourhood, velocityX, velocityY, values, scratch );
			compiled.Swap( values, scratch );
			compiled.Commit();
			for( int row = 0; row < Size; row++ ) {
				for( int column = 0; column < Size; column++ ) {
					_values[column, row] = grid.GetCell( column, row );
				}
			}
			_op.ScaleToRange( _values, _pixels );
			builder.AddFrame( _pixels );
		}
		builder.FinishAnimation();
	}

	private readonly struct IdentityBinding : IGridLayerBinding<float, float> {
		float IGridLayerBinding<float, float>.Extract( in float cell ) => cell;
		void IGridLayerBinding<float, float>.Commit( ref float cell, float value ) => cell = value;
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
}
