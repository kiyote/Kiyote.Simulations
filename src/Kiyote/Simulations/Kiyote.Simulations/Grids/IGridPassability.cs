namespace Kiyote.Simulations.Grids;

// Position-based passability: governs which cells things may occupy or move
// through (e.g. gas advection), independent of any velocity-reflection behavior.
public interface IGridPassability {

	bool IsSolid(
		int column,
		int row
	);

}
