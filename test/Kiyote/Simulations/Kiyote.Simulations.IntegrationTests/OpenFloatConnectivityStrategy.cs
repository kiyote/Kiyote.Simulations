using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.IntegrationTests; 

public sealed class OpenFloatConnectivityStrategy : IConnectivityStrategy<float> {
	bool IConnectivityStrategy<float>.Evaluate(
		GridCell<float> source,
		GridCell<float> destination,
		Direction direction,
		GridCell<float> orthogonalA,
		GridCell<float> orthogonalB
	) {
		return true;
	}
}
