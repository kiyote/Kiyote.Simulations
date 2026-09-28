namespace Kiyote.Simulations.Vectorized.Projection;

// Removes the divergent part of a velocity field stored as separate X and Y fields.
// All fields must be compiled against the same, unchanging topology; no version checks
// are performed. The pressure field is used as the warm start for the solve and holds
// the solved pressure on return; pressureScratch is working storage.
public interface IGridProjection {

	void Update<TCell>(
		Field<TCell> sourceVelocityX,
		Field<TCell> sourceVelocityY,
		Field<TCell> destinationVelocityX,
		Field<TCell> destinationVelocityY,
		Field<TCell> pressure,
		Field<TCell> pressureScratch
	);

}
