using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.LowFidelity.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;

internal sealed class AtmosphereVisualizer {

	public const int TotalFrameCount = 100;
	public const float PumpRate = 10.0f;

	private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds( 100 );

	private readonly IAnimationWriter _animation;
	private readonly IGridAtmospherics _atmospherics;
	private readonly IFileSystem _fileSystem;
	private readonly INumericBufferOperator _op;
	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _values;

	public AtmosphereVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op,
		IGridAtmospherics atmospherics
	) {
		_atmospherics = atmospherics;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_pixels = bufferFactory.Create<byte>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
		_values = bufferFactory.Create<float>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		IGridAtmospherics atmospherics = _atmospherics;

		DenseGridSource<AtmosphereCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		IGridAssembly<AtmosphereCell> assembly = new GridAssembly<AtmosphereCell>();
		if( !assembly.TryAttach( ship, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		using IAtmosphere atmosphere = atmospherics.Create( assembly, new AtmosphereCellStrategy() );
		(int Column, int Row, GasIndex Gas)[] pumps = [
			( 18, 7, atmosphere.Gases.GetIndex( "O2" ) ),
			( 17, 7, atmosphere.Gases.GetIndex( "N2" ) )
		];

		string fileName = _fileSystem.Path.Combine( outputFolder, "lowfidelity_atmosphere_pressure.gif" );
		using IAnimationBuilder builder = _animation.StartAnimation( fileName, FrameTime );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			foreach( (int column, int row, GasIndex gas) in pumps ) {
				atmosphere.AddGas( column, row, gas, PumpRate * (float)FrameTime.TotalSeconds );
			}
			_ = atmosphere.Advance( FrameTime );

			IGridLayer<float> pressure = atmosphere.Pressure;
			for( int row = 0; row < AsciiGridSource.Height; row++ ) {
				for( int column = 0; column < AsciiGridSource.Width; column++ ) {
					_values[column, row] = ship.IsOccupied( column, row )
						? pressure[column, row]
						: 0.0f;
				}
			}
			_op.ScaleToRange( _values, _pixels );
			builder.AddFrame( _pixels );
		}
		builder.FinishAnimation();
	}
}
