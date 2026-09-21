using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Visualizer.Temperature;

[ExcludeFromCodeCoverage]
internal readonly struct HeatDiffusionStrategy : IDiffusionStrategy<double, double> {

	private readonly double _diffusivity;

	// diffusivity = thermalConductivity / ( density * specificHeatCapacity ), in m^2/s,
	// assuming 1m x 1m cells.
	public HeatDiffusionStrategy(
		double diffusivity
	) {
		_diffusivity = diffusivity;
	}

	public double CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount,
		double timeStep
	) {
		double rate = Math.Min( _diffusivity * timeStep, 1.0 );
		int divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * rate / divisor;
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
		return cell.Cell + delta;
	}

}
