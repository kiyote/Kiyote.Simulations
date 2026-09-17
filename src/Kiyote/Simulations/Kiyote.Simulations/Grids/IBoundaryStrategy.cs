namespace Kiyote.Simulations.Grids;

// Governs velocity behavior at hull walls, vents, and other boundaries/obstacles.
// Also serves as the position-based passability check for anything advected across
// the grid (see IGridPassability) - a boundary is always a passability, but a
// passability (e.g. gas-only obstacles) need not carry velocity-reflection behavior.
public interface IBoundaryStrategy : IGridPassability {

	Velocity ReflectVelocity(
		Velocity velocity,
		int column,
		int row
	);

}
