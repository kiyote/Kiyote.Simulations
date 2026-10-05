using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;

namespace Kiyote.Simulations.LowFidelity.Visualizer.Atmospherics;

internal readonly record struct AtmosphereCell(
	bool IsWalkable,
	bool IsGasPermeable,
	bool IsVacuum
);

internal readonly struct AtmosphereCellStrategy : IAtmosphereCellStrategy<AtmosphereCell> {
	public float GetCondensate( in AtmosphereCell cell, GasIndex gas ) => 0.0f;
	public float GetGas( in AtmosphereCell cell, GasIndex gas ) => 0.0f;
	public float GetTemperature( in AtmosphereCell cell ) => 273.15f;
	public bool IsPermeable( in AtmosphereCell cell ) => cell.IsGasPermeable;
	public void SetCondensate( ref AtmosphereCell cell, GasIndex gas, float amount ) { }
	public void SetGas( ref AtmosphereCell cell, GasIndex gas, float amount ) { }
	public void SetTemperature( ref AtmosphereCell cell, float temperature ) { }
}

internal sealed class AtmosphericsSettings : IAtmosphericsSettings {
	public float Acceleration { get; init; } = 2.0f;
	public float Friction { get; init; } = 0.5f;
	public float Conduction { get; init; } = 0.1f;
	public float HeatCapacity { get; init; } = 20.8f;
	public float CondensationRate { get; init; } = 0.05f;
	public float FixedTimeStep { get; init; } = 0.1f;
	public int MaxStepsPerAdvance { get; init; } = 160;
	public float WindScale { get; init; } = 1.0f;
}

internal sealed class GasDefinitionSource : IGasDefinitionSource {
	public IEnumerable<GasDefinition> GetDefinitions() {
		yield return new GasDefinition( "O2", "Oxygen", 90.2f, 54.4f );
		yield return new GasDefinition( "N2", "Nitrogen", 77.4f, 63.1f );
	}
}

internal static class AsciiGridSource {

	public const int Width = 50;
	public const int Height = 20;

	public const string Map1 = """
XXXXXXXXXXXXXXXXXXXX..............................
A__________________X..............................
A__________________X..............................
X__________________X..............................
X__________________X..............................
X__________________X..............................
X__________________OOOOOOOOOOOOOOOOOOOOOO.........
X__________________XXXXXXXXXXXXXXXXXXXXXO.........
X______________________________________XO.........
X__________________XXXXXXXXXXXX________XO.........
X__________________X..........X________XO.........
X__________________X..........X________XO.........
X__________________X..........X________XO.........
XXXXXXXXXXXXXXXXXXXX..........X________XOXXXXXXXXX
..............................X__________________A
..............................X__________________A
..............................XXXXXXXXXX_________X
.......................................X_________X
.......................................XXXXXXXXXXX
..................................................

""";

	public static DenseGridSource<AtmosphereCell> Create(
		string map
	) {
		string[] lines = map.Split( '\n' );
		DenseGridSource<AtmosphereCell> source = new DenseGridSource<AtmosphereCell>( Width, Height );
		for( int row = 0; row < lines.Length && row < source.Height; row++ ) {
			string line = lines[row].TrimEnd( '\r' );
			for( int column = 0; column < line.Length && column < source.Width; column++ ) {
				AtmosphereCell? cell = line[column] switch {
					'X' => new AtmosphereCell( IsWalkable: false, IsGasPermeable: false, IsVacuum: false ),
					'O' => new AtmosphereCell( IsWalkable: false, IsGasPermeable: true, IsVacuum: false ),
					'_' => new AtmosphereCell( IsWalkable: true, IsGasPermeable: true, IsVacuum: false ),
					'A' => new AtmosphereCell( IsWalkable: true, IsGasPermeable: false, IsVacuum: false ),
					_ => null
				};
				if( cell is AtmosphereCell value ) {
					_ = source.TrySetCell( column, row, value );
				}
			}
		}
		return source;
	}
}
