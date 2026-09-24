using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Visualizer.Diffusion;

internal sealed class DefaultFlowStrategy : IDiffusionStrategy<float, float> {

	float IDiffusionStrategy<float, float>.CalculateTransfer(
		GridCell<float> source,
		GridCell<float> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		if( sourceNeighborCount == 0 ) {
			return 0f;
		}

		float difference = source.Cell - destination.Cell;
		return difference / ( sourceNeighborCount + 1 );
	}

	float IDiffusionStrategy<float, float>.Combine(
		float left,
		float right
	) {
		return left + right;
	}

	float IDiffusionStrategy<float, float>.Negate(
		float value
	) {
		return -value;
	}

	float IDiffusionStrategy<float, float>.Apply(
		GridCell<float> cell,
		float delta
	) {
		return cell.Cell + delta;
	}

}
