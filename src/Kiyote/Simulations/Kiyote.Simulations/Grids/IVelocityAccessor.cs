namespace Kiyote.Simulations.Grids;

// Lets IGridFluid operate on an arbitrary TCell (which may carry velocity plus other
// per-cell simulation state) by reading/writing just the Velocity component.
public interface IVelocityAccessor<TCell> {

	Velocity GetVelocity(
		TCell cell
	);

	TCell WithVelocity(
		TCell cell,
		Velocity velocity
	);

}
