namespace Kiyote.Simulations.Vectorized.Diffusion;

// Advances a dense float field by one step. Assumes source and destination were
// compiled against the same, unchanging topology; performs no version checks.
public interface IGridDiffusion {

	void Update<TCell>(
		Field<TCell> source,
		Field<TCell> destination
	);

}
