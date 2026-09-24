using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Pressure;

public interface IGridPressure {

	// Advances the pressure simulation for a single gas species by one fixed time step
	// (as defined by the simulation's ISimulationClock). To model a mixed atmosphere,
	// call this once per gas species (each with its own source/destination grids), using
	// the same connectivity grid for all of them. To advance by a larger duration, call
	// this repeatedly, double-buffering source/destination between calls.
	void Update<TCell, TPressure, TFlow, TPressureStrategy>(
		IGrid<TPressure> source,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<TPressure> destination,
		TPressureStrategy pressure
	)
		where TPressureStrategy : IPressureStrategy<TPressure, TFlow>;

}
