using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.LowFidelity.Thermals;
using Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Thermals;

internal sealed class ThermalVisualizer {

	public const int TotalFrameCount = 100;
	public const float RoomTemperature = 294.15f;

	// 10s of animation at 10fps; each frame advances one simulated hour.
	// Thin aluminium is a poor emitter and absorber, so changes take hours to show.
	private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds( 100 );
	private static readonly TimeSpan SimulatedPerFrame = TimeSpan.FromHours( 1 );

	private readonly IAnimationWriter _animation;
	private readonly IGridThermals _thermals;
	private readonly IMaterialRegistry _materials;
	private readonly ThermalsSettings _settings;
	private readonly IFileSystem _fileSystem;
	private readonly INumericBuffer<byte> _pixels;

	public ThermalVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		IGridThermals thermals,
		IMaterialRegistry materials,
		ThermalsSettings settings
	) {
		_settings = settings;
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_thermals = thermals;
		_materials = materials;
		_pixels = bufferFactory.Create<byte>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		// Grid +Y is south, so north-east is (1, -1).
		Vector northEast = new Vector( 1.0f, -1.0f );
		Run( outputFolder, "lowfidelity_thermal_solar.gif", northEast, radiate: false, RoomTemperature, 600.0f );
		Run( outputFolder, "lowfidelity_thermal_radiation.gif", new Vector(), radiate: true, 150.0f, RoomTemperature );
		Run( outputFolder, "lowfidelity_thermal_combined.gif", northEast, radiate: true, 150.0f, 400.0f );
	}

	private void Run(
		string outputFolder,
		string name,
		Vector sunDirection,
		bool radiate,
		float minimum,
		float maximum
	) {
		_settings.StefanBoltzmann = radiate ? ThermalsSettings.StefanBoltzmannConstant : 0.0f;
		DenseGridSource<AtmosphereCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		IGridAssembly<AtmosphereCell> assembly = new GridAssembly<AtmosphereCell>();
		if( !assembly.TryAttach( ship, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		using IThermal thermal = _thermals.Create( assembly, new ThermalCellStrategy( _materials, RoomTemperature ) );
		thermal.SunDirection = sunDirection;

		string fileName = _fileSystem.Path.Combine( outputFolder, name );
		using IAnimationBuilder builder = _animation.StartAnimation( fileName, FrameTime );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			_ = thermal.Advance( SimulatedPerFrame );

			IGridLayer<float> temperature = thermal.Temperature;
			for( int row = 0; row < AsciiGridSource.Height; row++ ) {
				for( int column = 0; column < AsciiGridSource.Width; column++ ) {
					_pixels[column, row] = ship.IsOccupied( column, row )
						? ToPixel( temperature[column, row], minimum, maximum )
						: (byte)0;
				}
			}
			builder.AddFrame( _pixels );
		}
		builder.FinishAnimation();
	}

	// Fixed scale so frames are comparable; 1 is reserved as the coldest visible structure.
	private static byte ToPixel(
		float temperature,
		float minimum,
		float maximum
	) {
		float t = ( temperature - minimum ) / ( maximum - minimum );
		return (byte)( 1 + ( Math.Clamp( t, 0.0f, 1.0f ) * 254.0f ) );
	}
}
