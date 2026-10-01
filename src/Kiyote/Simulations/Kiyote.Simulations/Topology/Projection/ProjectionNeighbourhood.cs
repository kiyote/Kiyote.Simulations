using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Projection;

// Caller-owned neighbour description used by GridProjection, built once per topology by
// IGridProjection.CreateNeighbourhood. Passable is the connectivity layer it was built from
// (flagged = flow allowed to an existing cell). Combined additionally flags every direction
// whose neighbour cell is missing (vacuum). An unflagged direction in Combined is a wall.
public sealed class ProjectionNeighbourhood {

	public ProjectionNeighbourhood(
		IGridLayer<Direction> passable,
		IGridLayer<Direction> combined
	) {
		Passable = passable;
		Combined = combined;
	}

	public IGridLayer<Direction> Passable { get; }

	public IGridLayer<Direction> Combined { get; }

}
