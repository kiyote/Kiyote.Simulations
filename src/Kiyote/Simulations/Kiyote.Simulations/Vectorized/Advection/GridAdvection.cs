using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Advection;

public sealed class GridAdvection : IGridAdvection {

	private const int North = 0;
	private const int East = 2;
	private const int SouthEast = 3;
	private const int South = 4;
	private const int West = 6;

	private readonly float _timeStep;

	public GridAdvection(
		ISimulationClock clock
	) {
		_timeStep = clock.FixedTimeStep;
	}

	void IGridAdvection.Update<TCell>(
		AdvectionNeighbourhood<TCell> neighbourhood,
		Field<TCell> velocityX,
		Field<TCell> velocityY,
		Field<TCell> source,
		Field<TCell> destination
	) {
		ReadOnlySpan<int> neighbours = neighbourhood.Neighbours;
		ReadOnlySpan<Direction> cells = neighbourhood.Topology.Cells;
		ReadOnlySpan<float> vx = velocityX.Values;
		ReadOnlySpan<float> vy = velocityY.Values;
		ReadOnlySpan<float> input = source.Values;
		Span<float> output = destination.Values;
		float timeStep = _timeStep;

		for( int index = 0; index < output.Length; index++ ) {
			float self = input[index];
			if( cells[index] == Direction.None ) {
				output[index] = self;
				continue;
			}

			float dx = -vx[index] * timeStep;
			float dy = -vy[index] * timeStep;
			if( ( dx == 0f && dy == 0f ) || !float.IsFinite( dx ) || !float.IsFinite( dy ) ) {
				output[index] = self;
				continue;
			}

			// Walk the neighbour table to the cell containing the backtraced position, so
			// displacements of more than one cell are interpolated rather than truncated.
			// A blocked step stops that axis at the last reachable cell.
			float floorX = MathF.Floor( dx );
			float floorY = MathF.Floor( dy );
			float fx = dx - floorX;
			float fy = dy - floorY;
			int remainingX = Math.Abs( (int)floorX );
			int remainingY = Math.Abs( (int)floorY );
			int horizontalSlot = floorX < 0f ? West : East;
			int verticalSlot = floorY < 0f ? North : South;
			int cell = index;
			while( remainingX > 0 || remainingY > 0 ) {
				bool moveX = remainingX >= remainingY;
				int next = neighbours[( cell * AdvectionNeighbourhood<TCell>.DirectionCount ) + ( moveX ? horizontalSlot : verticalSlot )];
				if( next < 0 ) {
					if( moveX ) {
						remainingX = 0;
						fx = 0f;
					} else {
						remainingY = 0;
						fy = 0f;
					}
					continue;
				}
				cell = next;
				if( moveX ) {
					remainingX--;
				} else {
					remainingY--;
				}
			}

			int baseIndex = cell * AdvectionNeighbourhood<TCell>.DirectionCount;
			int eastIndex = neighbours[baseIndex + East];
			int southIndex = neighbours[baseIndex + South];
			int southEastIndex = neighbours[baseIndex + SouthEast];

			float origin = input[cell];
			float east = eastIndex >= 0 ? input[eastIndex] : origin;
			float south = southIndex >= 0 ? input[southIndex] : origin;
			float southEast;
			if( southEastIndex >= 0 ) {
				southEast = input[southEastIndex];
			} else if( southIndex < 0 ) {
				southEast = east;
			} else if( eastIndex < 0 ) {
				southEast = south;
			} else {
				southEast = origin;
			}

			float near = origin + ( ( east - origin ) * fx );
			float far = south + ( ( southEast - south ) * fx );
			output[index] = near + ( ( far - near ) * fy );
		}
	}

}
