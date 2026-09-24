using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Pressure.IntegrationTests;

public sealed class TestConnectivityStrategy : IConnectivityStrategy<float> {
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
