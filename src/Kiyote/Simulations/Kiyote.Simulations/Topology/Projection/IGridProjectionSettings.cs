namespace Kiyote.Simulations.Topology.Projection;

public interface IGridProjectionSettings {

	// Nonnegative number of Jacobi relaxation iterations per Update. Zero uses the warm-start pressure unchanged.
	int Iterations { get; }

}
