using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Projection;

// Removes the divergent part of a velocity field stored as separate X and Y layers.
// All layers must come from the supplied, unchanging compiled assembly; no version checks are
// performed. No halo is needed: neighbouring values are read across chunk borders directly.
// The pressure layer is the warm start for the solve and holds the solved pressure on
// return; pressureScratch (distinct from pressure) and divergence are caller-owned working storage.
// Odd iteration counts swap pressure/scratch storage through the supplied owner;
// layer identities/bindings stay fixed, but callers must reacquire spans after Update.
//
// Boundaries follow the Topology model: a wall (unflagged direction to an existing cell)
// reflects velocity and has zero-gradient pressure; vacuum (missing neighbour cell) has zero
// pressure and zero-gradient outflow velocity.
public interface IGridProjection {

	ProjectionNeighbourhood CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		ProjectionNeighbourhood neighbourhood,
		IGridLayer<float> sourceVelocityX,
		IGridLayer<float> sourceVelocityY,
		IGridLayer<float> destinationVelocityX,
		IGridLayer<float> destinationVelocityY,
		IGridLayer<float> pressure,
		IGridLayer<float> pressureScratch,
		IGridLayer<float> divergence
	);

}
