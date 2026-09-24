using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Pressure;

public interface IPressureStrategy<TPressure, TFlow> {

	// Returns the scalar pressure value represented by the cell, used to determine
	// the direction and magnitude of flow between neighboring cells.
	TFlow GetPressure(
		TPressure cell
	);

	// Calculates the amount of gas that flows from source to destination this time step,
	// based on the pressure difference between them and the elapsed deltaTime.
	TFlow CalculateFlow(
		GridCell<TPressure> source,
		GridCell<TPressure> destination,
		int sourceNeighborCount,
		int destinationNeighborCount,
		float deltaTime
	);

	TFlow Combine(
		TFlow left,
		TFlow right
	);

	TFlow Negate(
		TFlow value
	);

	TPressure Apply(
		GridCell<TPressure> cell,
		TFlow delta
	);

}
