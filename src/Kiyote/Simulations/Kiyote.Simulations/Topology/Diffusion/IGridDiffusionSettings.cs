namespace Kiyote.Simulations.Topology.Diffusion;

// Injected into the GridDiffusion constructor. Different settings require a
// different simulation instance.
public interface IGridDiffusionSettings {

	// Fraction of the difference moved across each open edge per step. Stable for
	// values up to 1/8 (a cell may have eight open neighbours).
	float Rate { get; }

}
