using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.Topology.Airflow;

// Layers the Topology Pressure, Advection and Projection simulations into one
// fixed-time-step update for a single gas species:
//   1. relax the species' pressure (pressureSource -> pressureDestination),
//   2. self-advect the bulk velocity (sourceVelocity -> intermediateVelocity),
//   3. accelerate intermediateVelocity down the species' pressure gradient,
//   4. project intermediateVelocity onto its divergence-free part (-> destinationVelocity),
//   5. advect the concentration along destinationVelocity.
// All layers must come from the same, unchanging compiled assembly and need a halo of at
// least 1 (projectionDivergence may have any halo). All layers, including the intermediate
// velocity and projection scratch, are caller-owned; the caller swaps source and
// destination layers afterwards. Call once per species, sharing the velocity layers.
public interface IGridAirflow {

	AirflowNeighbourhood CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	);

	void Update(
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
