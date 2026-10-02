using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.IntegrationTests;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;

public static class AsciiGridSource {

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

	public static DenseGridSource<TestCell> Create(
		string map
	) {
		string[] lines = map.Split( '\n' );
		DenseGridSource<TestCell> source = new DenseGridSource<TestCell>( 50, 20 );
		for( int row = 0; row < lines.Length && row < source.Height; row++ ) {
			string line = lines[row].TrimEnd( '\r' );
			for( int column = 0; column < line.Length && column < source.Width; column++ ) {
				TestCell? cell = line[column] switch {
					'X' => new TestCell( IsWalkable: false, IsGasPermeable: false, IsVacuum: false ),
					'O' => new TestCell( IsWalkable: false, IsGasPermeable: true, IsVacuum: false ),
					'_' => new TestCell( IsWalkable: true, IsGasPermeable: true, IsVacuum: false ),
					'A' => new TestCell( IsWalkable: true, IsGasPermeable: false, IsVacuum: false ),
					_ => null
				};
				if( cell is TestCell value ) {
					_ = source.TrySetCell( column, row, value );
				}
			}
		}
		return source;
	}
}
