using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Advection;

// Semi-Lagrangian advection of source along (velocityX, velocityY) into destination.
// Each cell traces backward by velocity * FixedTimeStep, walking cell by cell to the
// backtraced position, and bilinearly samples the 2x2 cells around it. All layers must come
// from the same, unchanging compiled assembly; no version checks are performed. No halo is
// needed: the walk and sample cross chunk borders directly, and the caller can Swap source
// and destination afterwards.
//
// Boundaries follow the Topology model: a wall stops the walk along that axis at the last
// reachable cell, and a wall sample falls back to the nearest available value along the
// same axis. Vacuum (a missing neighbour cell) has value 0, so a walk that reaches vacuum
// samples 0 and vacuum sample neighbours contribute 0.
public interface IGridAdvection {

	AdvectionNeighbourhood CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update(
		AdvectionNeighbourhood neighbourhood,
		IGridLayer<float> velocityX,
		IGridLayer<float> velocityY,
		IGridLayer<float> source,
		IGridLayer<float> destination
	);

}
