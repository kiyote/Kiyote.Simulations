namespace Kiyote.Simulations.Vectorized.Advection;

public interface IGridAdvection {

	// Semi-Lagrangian advection of source along (velocityX, velocityY) into destination.
	// Each cell traces backward by velocity * FixedTimeStep, walking the neighbourhood
	// cell by cell to the backtraced position, and bilinearly samples the 2x2 cells
	// around it. Walks and samples follow seams into neighbouring leaves; a blocked
	// step (closed, outside, or wall cell) stops that axis at the last reachable cell,
	// and unavailable sample neighbours fall back to the nearest available value along
	// the same axis. Wall cells copy through.
	void Update<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> velocityX,
		Field<TCell> velocityY,
		Field<TCell> source,
		Field<TCell> destination
	);

}
