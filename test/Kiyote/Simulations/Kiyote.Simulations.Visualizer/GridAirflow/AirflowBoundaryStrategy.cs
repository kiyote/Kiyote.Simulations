using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridAirflow;

// Describes two rectangular rooms connected by a doorway; everything outside
// those three regions is solid hull. The doorway only counts as passable
// once the supplied DoorState reports it as open, letting the visualizer
// simulate a door being opened partway through the run.
[ExcludeFromCodeCoverage]
internal readonly struct AirflowBoundaryStrategy : IBoundaryStrategy {

	private readonly (int Left, int Top, int Right, int Bottom) _roomA;
	private readonly (int Left, int Top, int Right, int Bottom) _roomB;
	private readonly (int Left, int Top, int Right, int Bottom) _door;
	private readonly DoorState _doorState;

	public AirflowBoundaryStrategy(
		(int Left, int Top, int Right, int Bottom) roomA,
		(int Left, int Top, int Right, int Bottom) roomB,
		(int Left, int Top, int Right, int Bottom) door,
		DoorState doorState
	) {
		_roomA = roomA;
		_roomB = roomB;
		_door = door;
		_doorState = doorState;
	}

	public bool IsSolid(
		int column,
		int row
	) {
		if( Contains( _roomA, column, row ) || Contains( _roomB, column, row ) ) {
			return false;
		}

		if( Contains( _door, column, row ) ) {
			return !_doorState.IsOpen;
		}

		return true;
	}

	// Bounces velocity off the hull elastically: momentum heading into a wall
	// is reflected back into the room instead of being absorbed/vanished.
	public Velocity ReflectVelocity(
		Velocity velocity,
		int column,
		int row
	) {
		return -velocity;
	}

	private static bool Contains(
		(int Left, int Top, int Right, int Bottom) bounds,
		int column,
		int row
	) {
		return column >= bounds.Left
			&& column <= bounds.Right
			&& row >= bounds.Top
			&& row <= bounds.Bottom;
	}

}
