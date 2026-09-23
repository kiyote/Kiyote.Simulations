using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Visualizer.Diffusion;

internal sealed class AlwaysConnectedStrategy : IConnectivityStrategy<double> {
	bool IConnectivityStrategy<double>.Evaluate( GridCell<double> source, GridCell<double> destination, Direction direction, GridCell<double> orthogonalA, GridCell<double> orthogonalB ) {
		return true;
	}
}
