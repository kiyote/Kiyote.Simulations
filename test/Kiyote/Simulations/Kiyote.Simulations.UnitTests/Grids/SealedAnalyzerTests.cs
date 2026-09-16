using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids.UnitTests;

[ExcludeFromCodeCoverage]
internal readonly struct CharPassabilityStrategy : ICellStrategy<char, bool> {

	public bool Evaluate(
		GridCell<char> cell
	) {
		return cell.Cell != '#';
	}

}

[TestFixture]
[ExcludeFromCodeCoverage]
internal sealed class SealedAnalyzerTests {

	private ISealedAnalyzer _analyzer;

	[SetUp]
	public void SetUp() {
		_analyzer = new SealedAnalyzer();
	}

	private static TestGrid<char> CreateGrid(
		string[] rows
	) {
		TestGrid<char> grid = new TestGrid<char>( 0, 0, rows[0].Length, rows.Length );
		for( int row = 0; row < rows.Length; row++ ) {
			for( int column = 0; column < rows[row].Length; column++ ) {
				grid.Set( column, row, rows[row][column] );
			}
		}
		return grid;
	}

	[Test]
	public void IsSealed_FullyWalledIncludingCorners_ReturnsTrue() {
		TestGrid<char> grid = CreateGrid( [
			"###",
			"#.#",
			"###",
		] );

		bool result = _analyzer.IsSealed( grid, 1, 1, new CharPassabilityStrategy() );

		Assert.That( result, Is.True );
	}

	[Test]
	public void IsSealed_OpenCornerAllowsDiagonalEscape_ReturnsFalse() {
		TestGrid<char> grid = CreateGrid( [
			".##",
			"#.#",
			"###",
		] );

		bool result = _analyzer.IsSealed( grid, 1, 1, new CharPassabilityStrategy() );

		Assert.That( result, Is.False );
	}

	[Test]
	public void IsSealed_GapInWall_ReturnsFalse() {
		TestGrid<char> grid = CreateGrid( [
			"###",
			"#..",
			"###",
		] );

		bool result = _analyzer.IsSealed( grid, 1, 1, new CharPassabilityStrategy() );

		Assert.That( result, Is.False );
	}

	[Test]
	public void IsSealed_StartOutsideBounds_ReturnsFalse() {
		TestGrid<char> grid = CreateGrid( [
			"###",
			"#.#",
			"###",
		] );

		bool result = _analyzer.IsSealed( grid, -1, -1, new CharPassabilityStrategy() );

		Assert.That( result, Is.False );
	}

	[Test]
	public void IsSealed_InteriorWallTouchingDiagonally_StillSealed() {
		TestGrid<char> grid = CreateGrid( [
			"#####",
			"#...#",
			"#.#.#",
			"#...#",
			"#####",
		] );

		bool result = _analyzer.IsSealed( grid, 1, 1, new CharPassabilityStrategy() );

		Assert.That( result, Is.True );
	}

}
