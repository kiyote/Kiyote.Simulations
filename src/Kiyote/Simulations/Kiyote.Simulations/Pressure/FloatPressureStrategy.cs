using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Pressure;

public sealed class FloatPressureStrategy : IPressureStrategy<float, float> {
	float IPressureStrategy<float, float>.GetPressure(
		float cell
	) {
		return cell;
	}

	float IPressureStrategy<float, float>.CalculateFlow(
		GridCell<float> source,
		GridCell<float> destination,
		int sourceNeighborCount,
		int destinationNeighborCount,
		float deltaTime
	) {
		if( sourceNeighborCount == 0 ) {
			return 0f;
		}

		float difference = source.Cell - destination.Cell;
		return difference / ( sourceNeighborCount + 1 ) * deltaTime;
	}

	float IPressureStrategy<float, float>.Combine(
		float left,
		float right
	) {
		return left + right;
	}

	float IPressureStrategy<float, float>.Negate(
		float value
	) {
		return -value;
	}

	float IPressureStrategy<float, float>.Apply(
		GridCell<float> cell,
		float delta
	) {
		return cell.Cell + delta;
	}
}
