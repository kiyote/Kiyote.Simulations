using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Visualizer.Diffusion;

internal sealed class DefaultFlowStrategy : IDiffusionStrategy<double, double> {

	double IDiffusionStrategy<double, double>.CalculateTransfer(
		GridCell<double> source,
		GridCell<double> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		if( sourceNeighborCount == 0 ) {
			return 0d;
		}

		double difference = source.Cell - destination.Cell;
		return difference / ( sourceNeighborCount + 1 );
	}

	double IDiffusionStrategy<double, double>.Combine(
		double left,
		double right
	) {
		return left + right;
	}

	double IDiffusionStrategy<double, double>.Negate(
		double value
	) {
		return -value;
	}

	double IDiffusionStrategy<double, double>.Apply(
		GridCell<double> cell,
		double delta
	) {
		return cell.Cell + delta;
	}

}
