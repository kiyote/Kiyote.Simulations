namespace Kiyote.Simulations.Vectorized.Projection;

// Removes the divergent part of a velocity field stored as separate X and Y fields.
// All fields and the neighbourhood must be built against the same, unchanging topology; no version checks
// are performed. The pressure field is used as the warm start for the solve and holds
// the solved pressure on return; pressureScratch and divergence are working storage.
public interface IGridProjection {

	void Update<TCell>(
		ProjectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> sourceVelocityX,
		Field<TCell> sourceVelocityY,
		Field<TCell> destinationVelocityX,
		Field<TCell> destinationVelocityY,
		Field<TCell> pressure,
		Field<TCell> pressureScratch,
		Field<TCell> divergence
	);

}
