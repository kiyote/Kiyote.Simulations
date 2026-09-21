using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.Grids.GridPressure;

// A rate-based Fickian diffusion strategy for a gas species' partial pressure,
// mirroring Visualizer.GridDiffusion.DiffusionStrategy.
[ExcludeFromCodeCoverage]
internal readonly struct PressureDiffusionStrategy : IDiffusionStrategy<double, double> {

	private readonly double _rate;

	public PressureDiffusionStrategy() : this( 1.0 ) { }

	// rate ranges from 0 (no flow) to 1 (fully equalizes in a single step).
	public PressureDiffusionStrategy(
		double rate
	) {
		_rate = rate;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * _rate / divisor;
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
		return Math.Max( 0.0, cell.Cell + delta );
	}

}
