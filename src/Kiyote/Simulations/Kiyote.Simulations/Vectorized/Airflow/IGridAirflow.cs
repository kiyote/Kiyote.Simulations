using Kiyote.Simulations.Vectorized.Advection;

namespace Kiyote.Simulations.Vectorized.Airflow;

// Vectorized counterpart of Kiyote.Simulations.Airflow.IGridAirflow. Layers the
// vectorized Pressure, Advection and Projection simulations into one fixed-time-step
// update for a single gas species:
//   1. relax the species' pressure (pressureSource -> pressureDestination),
//   2. self-advect the bulk velocity (sourceVelocity -> intermediateVelocity),
//   3. accelerate intermediateVelocity down the species' pressure gradient,
//   4. project intermediateVelocity onto its divergence-free part (-> destinationVelocity),
//   5. advect the concentration along destinationVelocity.
// All buffers, including the intermediate velocity and projection scratch, are
// caller-owned. Call once per species, sharing the velocity fields between species.
public interface IGridAirflow {

	void Update<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> pressureSource,
		Field<TCell> pressureDestination,
		Field<TCell> sourceVelocityX,
		Field<TCell> sourceVelocityY,
		Field<TCell> destinationVelocityX,
		Field<TCell> destinationVelocityY,
		Field<TCell> intermediateVelocityX,
		Field<TCell> intermediateVelocityY,
		Field<TCell> projectionPressure,
		Field<TCell> projectionPressureScratch,
		Field<TCell> concentrationSource,
		Field<TCell> concentrationDestination
	);

}
