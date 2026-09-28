namespace Kiyote.Simulations.Vectorized;

// Extracts the float a simulation operates on from a TCell, and writes it back.
public interface IFieldSelector<TCell> {

	float GetValue(
		TCell cell
	);

	TCell SetValue(
		TCell cell,
		float value
	);

}
