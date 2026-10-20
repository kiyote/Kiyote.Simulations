using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Airflow;

// Layers the Topology Pressure, Advection and Projection simulations into one
// fixed-time-step update for a single gas species:
//   1. relax the species' pressure (pressureSource -> pressureDestination),
//   2. self-advect the bulk velocity (sourceVelocity -> intermediateVelocity),
//   3. accelerate intermediateVelocity down the species' pressure gradient,
//   4. project intermediateVelocity onto its divergence-free part (-> destinationVelocity),
//   5. advect the concentration along destinationVelocity.
// All layers must come from the supplied, unchanging compiled assembly; no halo is needed, as
// neighbouring values are read across chunk borders directly. All layers, including the intermediate
// velocity and projection scratch, are caller-owned; the caller swaps source and
// destination layers afterwards. Call once per species, sharing the velocity layers.
// Projection can swap its pressure/scratch storage through the supplied owner.
// Reacquire layer spans after Update.
public interface IGridAirflow {

	AirflowNeighbourhood CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		AirflowNeighbourhood neighbourhood,
		IGridLayer<float> pressureSource,
		IGridLayer<float> pressureDestination,
		IGridLayer<float> sourceVelocityX,
		IGridLayer<float> sourceVelocityY,
		IGridLayer<float> destinationVelocityX,
		IGridLayer<float> destinationVelocityY,
		IGridLayer<float> intermediateVelocityX,
		IGridLayer<float> intermediateVelocityY,
		IGridLayer<float> projectionPressure,
		IGridLayer<float> projectionPressureScratch,
		IGridLayer<float> projectionDivergence,
		IGridLayer<float> concentrationSource,
		IGridLayer<float> concentrationDestination
	);

}
