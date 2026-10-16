using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Diffusion;

// Advances a chunked float layer by one step, reading source and writing destination.
// All layers must come from the same, unchanging compiled assembly; no version checks
// are performed. No halo is needed: neighbouring values are read across chunk borders
// directly, and the caller can ICompiledGridAssembly.Swap source and destination afterwards.
//
// For each of a cell's eight directions:
//   - flagged in connectivity: the neighbour exchanges normally;
//   - unflagged, neighbour occupied: a wall, nothing crosses;
//   - unflagged, neighbour unoccupied: vacuum, anything moving that way is discarded.
// The neighbourhood is connectivity | vacuum, built once per topology by
// CreateNeighbourhood. Unoccupied cells hold no directions and read as 0, so they stay 0
// without masking. Missing chunks read as 0. Update marks every written destination chunk
// dirty.
public interface IGridDiffusion {

	// Builds the combined passable-or-vacuum layer. The caller owns the returned layer and
	// should RemoveLayer and rebuild it after the topology changes.
	IGridLayer<Direction> CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> source,
		IGridLayer<float> destination
	);

}
