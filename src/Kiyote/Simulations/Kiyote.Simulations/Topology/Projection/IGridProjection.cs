using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Projection;

// Removes the divergent part of a velocity field stored as separate X and Y layers.
// All layers must come from the same, unchanging compiled assembly; no version checks are
// performed. Velocity and pressure layers need a halo of at least 1; the source and
// destination velocity layers must share a halo, as must pressure and pressureScratch.
// The pressure layer is the warm start for the solve and holds the solved pressure on
// return; pressureScratch and divergence are caller-owned working storage.
//
// Boundaries follow the Topology model: a wall (unflagged direction to an existing cell)
// reflects velocity and has zero-gradient pressure; vacuum (missing neighbour cell) has zero
// pressure and zero-gradient outflow velocity.
public interface IGridProjection {

	ProjectionNeighbourhood CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update(
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
