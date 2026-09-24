using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Visualizer.Pressure;

internal sealed class AlwaysConnectedStrategy : IConnectivityStrategy<float> {
	bool IConnectivityStrategy<float>.Evaluate( GridCell<float> source, GridCell<float> destination, Direction direction, GridCell<float> orthogonalA, GridCell<float> orthogonalB ) {
		return true;
	}
}
