using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Pressure;

// Advances a chunked pressure layer by one fixed time step, reading source and writing
// destination. All layers must come from the same, unchanging compiled assembly; no version
// checks are performed. Source and destination need a halo of at least 1 and must share a
// halo so the caller can ICompiledGridAssembly.Swap them afterwards.
//
// Boundaries follow the Topology model: flagged directions equalise, unflagged directions to
// existing cells are walls, and directions to missing cells are vacuum (pressure 0), so
// pressure bleeds away into them. The neighbourhood is built once per topology by
// CreateNeighbourhood; the caller owns it and should rebuild it after the topology changes.
public interface IGridPressure {

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
