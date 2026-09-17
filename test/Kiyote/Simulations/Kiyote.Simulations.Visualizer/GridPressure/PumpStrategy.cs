using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridPressure;

// Injects a fixed amount of a gas species' partial pressure per second at a
// single cell (a pump), or extracts it (a vent) when the rate is negative.
[ExcludeFromCodeCoverage]
internal readonly struct PumpStrategy : IPumpStrategy<double, double> {

	private readonly int _column;
	private readonly int _row;
	private readonly double _ratePerSecond;

	public PumpStrategy(
		int column,
		int row,
		double ratePerSecond
	) {
		_column = column;
		_row = row;
		_ratePerSecond = ratePerSecond;
	}

	public double Apply(
		GridCell<double> cell,
		double concentration,
		double timeStep
	) {
		if( cell.Column != _column || cell.Row != _row ) {
			return concentration;
		}

		return Math.Max( 0.0, concentration + ( _ratePerSecond * timeStep ) );
	}

}
