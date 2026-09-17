using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer;

[ExcludeFromCodeCoverage]
internal readonly struct DiffusionStrategy : IDiffusionStrategy<double, double> {

	private readonly double _viscosity;

	public DiffusionStrategy() : this( 1.0 ) { }

	// viscosity ranges from 0 (no flow) to 1 (fully equalizes in a single step).
	public DiffusionStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * _viscosity / divisor;
	}

	public double Combine(
		double left,
		double right
	) {
		return left + right;
	}

	public double Negate(
		double value
	) {
		return -value;
	}

	public double Apply(
		GridCell<double> cell,
		double delta
	) {
		return Math.Max( 0, cell.Cell + delta );
	}

}
