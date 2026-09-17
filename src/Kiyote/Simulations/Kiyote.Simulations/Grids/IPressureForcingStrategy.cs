using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Converts a total-pressure difference between two adjacent cells into a
// Velocity impulse (a local -deltaP body force), which IGridAirflow applies to
// both cells' velocity fields so bulk airflow emerges from pressure gradients
// rather than from molecular diffusion alone.
public interface IPressureForcingStrategy {

	Velocity CalculateForce(
		GridCell<double> source,
		GridCell<double> destination
	);

	Velocity Combine(
		Velocity left,
		Velocity right
	);

}
