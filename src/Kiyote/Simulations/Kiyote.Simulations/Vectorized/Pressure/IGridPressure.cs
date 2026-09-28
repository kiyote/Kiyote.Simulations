namespace Kiyote.Simulations.Vectorized.Pressure;

// Advances a dense pressure field by one fixed time step. Assumes source and destination
// were compiled against the same, unchanging topology; performs no version checks.
public interface IGridPressure {

	void Update<TCell>(
		Field<TCell> source,
		Field<TCell> destination
	);

}
