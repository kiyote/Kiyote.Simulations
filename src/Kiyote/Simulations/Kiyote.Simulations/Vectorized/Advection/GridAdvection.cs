using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Vectorized.Advection;

public sealed class GridAdvection : IGridAdvection {

	private const int North = 0;
	private const int NorthEast = 1;
	private const int East = 2;
	private const int SouthEast = 3;
	private const int South = 4;
	private const int SouthWest = 5;
	private const int West = 6;
	private const int NorthWest = 7;

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

			float dx = Math.Clamp( -vx[index] * timeStep, -1f, 1f );
			float dy = Math.Clamp( -vy[index] * timeStep, -1f, 1f );
			float fx = MathF.Abs( dx );
			float fy = MathF.Abs( dy );
			if( fx == 0f && fy == 0f ) {
				output[index] = self;
				continue;
			}

			int baseIndex = index * AdvectionNeighbourhood<TCell>.DirectionCount;
			int horizontalSlot = dx < 0f ? West : East;
			int verticalSlot = dy < 0f ? North : South;
			int diagonalSlot = dy < 0f
				? ( dx < 0f ? NorthWest : NorthEast )
				: ( dx < 0f ? SouthWest : SouthEast );

			int horizontalIndex = neighbours[baseIndex + horizontalSlot];
			int verticalIndex = neighbours[baseIndex + verticalSlot];
			int diagonalIndex = neighbours[baseIndex + diagonalSlot];

			float horizontal = horizontalIndex >= 0 ? input[horizontalIndex] : self;
			float vertical = verticalIndex >= 0 ? input[verticalIndex] : self;
			float diagonal;
			if( diagonalIndex >= 0 ) {
				diagonal = input[diagonalIndex];
			} else if( verticalIndex < 0 ) {
				diagonal = horizontal;
			} else if( horizontalIndex < 0 ) {
				diagonal = vertical;
			} else {
				diagonal = self;
			}

			float near = self + ( ( horizontal - self ) * fx );
			float far = vertical + ( ( diagonal - vertical ) * fx );
			output[index] = near + ( ( far - near ) * fy );
		}
	}

}
