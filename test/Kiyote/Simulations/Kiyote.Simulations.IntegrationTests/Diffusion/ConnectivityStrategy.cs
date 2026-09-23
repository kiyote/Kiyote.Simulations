using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.IntegrationTests.Diffusion;

public sealed class ConnectivityStrategy : IConnectivityStrategy<double> {
	bool IConnectivityStrategy<double>.Evaluate(
		GridCell<double> source,
		GridCell<double> destination,
		Direction direction,
		GridCell<double> orthogonalA,
		GridCell<double> orthogonalB
	) {
		return true;
	}
}
