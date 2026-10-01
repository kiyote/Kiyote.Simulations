using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Advection;

// Caller-owned neighbour description used by GridAdvection, built once per topology by
// IGridAdvection.CreateNeighbourhood. Passable is the connectivity layer it was built from
// (flagged = flow allowed to an existing cell). Vacuum flags every direction whose neighbour
// cell is missing. A direction flagged in neither is a wall.
public sealed class AdvectionNeighbourhood {

	public AdvectionNeighbourhood(
		IGridLayer<Direction> passable,
		IGridLayer<Direction> vacuum
	) {
		Passable = passable;
		Vacuum = vacuum;
	}

	public IGridLayer<Direction> Passable { get; }

	public IGridLayer<Direction> Vacuum { get; }

}
