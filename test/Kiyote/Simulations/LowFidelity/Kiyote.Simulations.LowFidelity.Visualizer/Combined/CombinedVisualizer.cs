using System.IO.Abstractions;
using Kiyote.Buffers.Numerics;
using Kiyote.Geometry.Topology;
using Kiyote.Imaging;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;
using Kiyote.Simulations.LowFidelity.Visualizer.Thermals;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Combined;

// Runs the atmosphere and the structure's thermal simulation side by side on the same ship,
// coupling them with convection: heat moves between each gas cell and the structure it touches
// (its own floor plus any adjacent walls). The structure starts at -50C, is heated by the sun
// (north-east), cools by radiating to space, conducts through itself, and exchanges heat with
// the 21C air being pumped in.
internal sealed class CombinedVisualizer {

	public const int TotalFrameCount = 100;
	public const float RoomTemperature = 294.15f;
	public const float StructureTemperature = 223.15f;
	public const float PumpRate = 10.0f;

	// Convective conductance between gas and a structure face, W/K (h ~10 W/m^2K over 1m^2).
	public const float Convection = 10.0f;

	// 10s of animation at 10fps; each frame is one simulated minute, coupled every 10 seconds.
	private static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds( 100 );
	private static readonly TimeSpan SimulatedPerFrame = TimeSpan.FromMinutes( 1 );
	private static readonly TimeSpan CouplingInterval = TimeSpan.FromSeconds( 10 );

	private static readonly (int Column, int Row)[] Faces = [( 0, -1 ), ( 1, 0 ), ( 0, 1 ), ( -1, 0 )];

	private readonly IAnimationWriter _animation;
	private readonly IFileSystem _fileSystem;
	private readonly IGridAtmospherics _atmospherics;
	private readonly IGridThermals _thermals;
	private readonly IMaterialRegistry _materials;
	private readonly IAtmosphericsSettings _atmosphericsSettings;
	private readonly ThermalsSettings _thermalsSettings;
	private readonly INumericBufferOperator _op;
	private readonly INumericBuffer<byte> _pixels;
	private readonly INumericBuffer<float> _values;

	public CombinedVisualizer(
		IAnimationWriter animationWriter,
		IFileSystem fileSystem,
		INumericBufferFactory bufferFactory,
		INumericBufferOperator op,
		IGridAtmospherics atmospherics,
		IGridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		ThermalsSettings thermalsSettings
	) {
		_animation = animationWriter;
		_fileSystem = fileSystem;
		_op = op;
		_atmospherics = atmospherics;
		_thermals = thermals;
		_materials = materials;
		_atmosphericsSettings = atmosphericsSettings;
		_thermalsSettings = thermalsSettings;
		_pixels = bufferFactory.Create<byte>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
		_values = bufferFactory.Create<float>( AsciiGridSource.Width, AsciiGridSource.Height, 0 );
	}

	public void Execute(
		string outputFolder
	) {
		_thermalsSettings.StefanBoltzmann = ThermalsSettings.StefanBoltzmannConstant;

		DenseGridSource<AtmosphereCell> ship = AsciiGridSource.Create( AsciiGridSource.Map1 );
		bool[] walls = CreateWalls( AsciiGridSource.Map1 );
		IGridAssembly<AtmosphereCell> assembly = new GridAssembly<AtmosphereCell>();
		if( !assembly.TryAttach( ship, 0, 0 ).Succeeded ) {
			throw new InvalidOperationException( "Unable to attach grid." );
		}

		using IAtmosphere atmosphere = _atmospherics.Create( assembly, new AtmosphereCellStrategy() );
		using IThermal thermal = _thermals.Create( assembly, new ThermalCellStrategy( _materials, StructureTemperature ) );
		// Grid +Y is south, so north-east is (1, -1).
		thermal.SunDirection = new Vector( 1.0f, -1.0f );

		(int Column, int Row, GasIndex Gas)[] pumps = [
			( 18, 7, atmosphere.Gases.GetIndex( "O2" ) ),
			( 17, 7, atmosphere.Gases.GetIndex( "N2" ) )
		];

		int couplings = (int)( SimulatedPerFrame / CouplingInterval );
		float interval = (float)CouplingInterval.TotalSeconds;

		string fileName = _fileSystem.Path.Combine( outputFolder, "lowfidelity_combined_temperature.gif" );
		using IAnimationBuilder builder = _animation.StartAnimation( fileName, FrameTime );
		string gasFileName = _fileSystem.Path.Combine( outputFolder, "lowfidelity_combined_gas_temperature.gif" );
		using IAnimationBuilder gasBuilder = _animation.StartAnimation( gasFileName, FrameTime );
		for( int frame = 0; frame < TotalFrameCount; frame++ ) {
			for( int c = 0; c < couplings; c++ ) {
				foreach( (int column, int row, GasIndex gas) in pumps ) {
					atmosphere.AddGas( column, row, gas, PumpRate * interval, RoomTemperature );
				}
				_ = atmosphere.Advance( CouplingInterval );
				_ = thermal.Advance( CouplingInterval );
				Convect( ship, walls, atmosphere, thermal, interval );
			}

			Render( ship, thermal.Temperature, null, builder );
			Render( ship, atmosphere.Temperature, atmosphere, gasBuilder );
		}
		builder.FinishAnimation();
		gasBuilder.FinishAnimation();
	}

	private static bool[] CreateWalls(
		string map
	) {
		bool[] walls = new bool[AsciiGridSource.Width * AsciiGridSource.Height];
		string[] lines = map.Split( '\n' );
		for( int row = 0; row < lines.Length && row < AsciiGridSource.Height; row++ ) {
			string line = lines[row].TrimEnd( '\r' );
			for( int column = 0; column < line.Length && column < AsciiGridSource.Width; column++ ) {
				walls[( row * AsciiGridSource.Width ) + column] = line[column] is 'X' or 'A';
			}
		}
		return walls;
	}

	// Moves heat between each gas cell and the structure it touches: its own floor plus every
	// adjacent wall face. Each partner gets an equal share of the equilibrium cap so the gas
	// never overshoots the shared equilibrium temperature.
	private void Convect(
		DenseGridSource<AtmosphereCell> ship,
		bool[] walls,
		IAtmosphere atmosphere,
		IThermal thermal,
		float dt
	) {
		float structureCapacity = _materials.GetDefinition( _materials.GetIndex( "aluminium" ) ).HeatCapacity;
		IGridLayer<float> gasTemperature = atmosphere.Temperature;
		IGridLayer<float> structureTemperature = thermal.Temperature;
		Span<(int Column, int Row)> partners = stackalloc (int, int)[5];
		for( int row = 0; row < AsciiGridSource.Height; row++ ) {
			for( int column = 0; column < AsciiGridSource.Width; column++ ) {
				if( !ship.IsOccupied( column, row ) || walls[( row * AsciiGridSource.Width ) + column] ) {
					continue;
				}
				float total = atmosphere.GetTotalGas( column, row );
				if( total <= 0.0f ) {
					continue;
				}

				int count = 0;
				partners[count++] = (column, row);
				foreach( (int dc, int dr) in Faces ) {
					int c = column + dc;
					int r = row + dr;
					if( c >= 0 && c < AsciiGridSource.Width && r >= 0 && r < AsciiGridSource.Height && walls[( r * AsciiGridSource.Width ) + c] ) {
						partners[count++] = (c, r);
					}
				}

				float gasCapacity = total * _atmosphericsSettings.HeatCapacity;
				float gas = gasTemperature[column, row];
				for( int i = 0; i < count; i++ ) {
					(int c, int r) = partners[i];
					float difference = structureTemperature[c, r] - gas;
					float equilibrium = difference * gasCapacity * structureCapacity / ( gasCapacity + structureCapacity ) / count;
					float joules = Convection * difference * dt;
					if( MathF.Abs( joules ) > MathF.Abs( equilibrium ) ) {
						joules = equilibrium;
					}
					float applied = atmosphere.AddEnergy( column, row, joules );
					thermal.AddEnergy( c, r, -applied );
				}
			}
		}
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
