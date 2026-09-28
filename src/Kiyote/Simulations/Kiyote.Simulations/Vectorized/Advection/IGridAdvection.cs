namespace Kiyote.Simulations.Vectorized.Advection;

public interface IGridAdvection {

	// Semi-Lagrangian advection of source along (velocityX, velocityY) into destination.
	// Each cell traces backward by velocity * FixedTimeStep, with the displacement
	// clamped to at most one cell per axis, and bilinearly samples its 3x3
	// neighbourhood. Samples follow seams into neighbouring leaves; unavailable
	// neighbours (closed, outside, or wall cells) fall back to the nearest available
	// value along the same axis, or to the cell itself. Wall cells copy through.
	void Update<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> velocityX,
		Field<TCell> velocityY,
		Field<TCell> source,
		Field<TCell> destination
	);

}
