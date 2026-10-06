using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.LowFidelity.AtmosphericThermals;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;
using Kiyote.Simulations.LowFidelity.Visualizer.Thermals;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Combined;

// Runs the atmosphere and the structure's thermal simulation together on the same ship through
// IAtmosphericThermals, which exchanges heat between each gas cell and its floor and adjacent walls.
// The structure starts at -50C, is heated by the sun (north-east), cools by radiating to space,
// conducts through itself, and exchanges heat with the 21C air being pumped in.
internal sealed class CombinedVisualizer {

	public const int TotalFrameCount = 100;
	public const float RoomTemperature = 294.15f;
	public const float StructureTemperature = 223.15f;
	public const float PumpRate = 10.0f;

	// 10s of animation at 10fps; each frame is one simulated minute, pumped and updated every 10 seconds.
	private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds( 100 );
	private static readonly TimeSpan SimulatedPerFrame = TimeSpan.FromMinutes( 1 );
	private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds( 10 );

	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IGridAtmosphericThermals _atmosphericThermals;
	private readonly IMaterialRegistry _materials;
	private readonly ThermalsSettings _thermalsSettings;
	private readonly INumericBufferOperator _op;
	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _values;

	public CombinedVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op,
		IGridAtmosphericThermals atmosphericThermals,
		IMaterialRegistry materials,
		ThermalsSettings thermalsSettings
	) {
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_atmosphericThermals = atmosphericThermals;
		_materials = materials;
		_thermalsSettings = thermalsSettings;
		_pixels = bufferFactory.Create<byte>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
		_values = bufferFactory.Create<float>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		_thermalsSettings.StefanBoltzmann = ThermalsSettings.StefanBoltzmannConstant;

		DenseGridSource<AtmosphereCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		IGridAssembly<AtmosphereCell> assembly = new GridAssembly<AtmosphereCell>();
		if( !assembly.TryAttach( ship, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		using IAtmosphericThermals simulation = _atmosphericThermals.Create(
			assembly,
			new AtmosphereCellStrategy(),
			new ThermalCellStrategy( _materials, StructureTemperature )
		);
		IAtmosphere atmosphere = simulation.Atmosphere;
		IThermal thermal = simulation.Thermal;
		// Grid +Y is south, so north-east is (1, -1).
		thermal.SunDirection = new Vector( 1.0f, -1.0f );

		(int Column, int Row, GasIndex Gas)[] pumps = [
			( 18, 7, atmosphere.Gases.GetIndex( "O2" ) ),
			( 17, 7, atmosphere.Gases.GetIndex( "N2" ) )
		];

		int updates = (int)( SimulatedPerFrame / UpdateInterval );
		float interval = (float)UpdateInterval.TotalSeconds;

		string fileName = _fileSystem.Path.Combine( outputFolder, "lowfidelity_combined_temperature.gif" );
		using IAnimationBuilder builder = _animation.StartAnimation( fileName, FrameTime );
		string gasFileName = _fileSystem.Path.Combine( outputFolder, "lowfidelity_combined_gas_temperature.gif" );
		using IAnimationBuilder gasBuilder = _animation.StartAnimation( gasFileName, FrameTime );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int u = 0; u < updates; u++ ) {
				foreach( (int column, int row, GasIndex gas) in pumps ) {
					atmosphere.AddGas( column, row, gas, PumpRate * interval, RoomTemperature );
				}
				_ = simulation.Update( UpdateInterval );
			}

			Render( ship, thermal.Temperature, null, builder );
			Render( ship, atmosphere.Temperature, atmosphere, gasBuilder );
		}
		builder.FinishAnimation();
		gasBuilder.FinishAnimation();
	}

	// Scales the shown cells to the full palette. Hidden cells (vacuum, or no gas when an
	// atmosphere is given) take the frame minimum so they render black without skewing the range.
	private void Render(
		DenseGridSource<AtmosphereCell> ship,
		IGridLayer<float> temperature,
		IAtmosphere? atmosphere,
		IAnimationBuilder builder
	) {
		float minimum = float.MaxValue;
		for( int row = 0; row < AsciiGridSource.Height; row++ ) {
			for( int column = 0; column < AsciiGridSource.Width; column++ ) {
				if( IsShown( ship, atmosphere, column, row ) ) {
					minimum = MathF.Min( minimum, temperature[column, row] );
				}
			}
		}
		for( int row = 0; row < AsciiGridSource.Height; row++ ) {
			for( int column = 0; column < AsciiGridSource.Width; column++ ) {
				_values[column, row] = IsShown( ship, atmosphere, column, row )
					? temperature[column, row]
					: minimum;
			}
		}
		_op.ScaleToRange( _values, _pixels );
		builder.AddFrame( _pixels );
	}

	private static bool IsShown(
		DenseGridSource<AtmosphereCell> ship,
		IAtmosphere? atmosphere,
		int column,
		int row
	) {
		return ship.IsOccupied( column, row )
			&& ( atmosphere is null || atmosphere.GetTotalGas( column, row ) > 0.0f );
	}
}
