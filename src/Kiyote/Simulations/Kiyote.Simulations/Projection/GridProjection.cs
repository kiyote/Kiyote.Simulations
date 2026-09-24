using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Pressure;

namespace Kiyote.Simulations.Projection;

public sealed class GridProjection : IGridProjection {

	// Number of Jacobi relaxation iterations performed against the pressure-correction
	// field before it is considered converged enough to subtract its gradient from the
	// source velocity field.
	private const int Iterations = 20;

	// Unlike GridDiffusion (which accumulates a pairwise transfer once per edge and
	// applies it to both cells), divergence/gradient are computed independently per
	// cell, so every direction a cell's own connectivity flags may report is walked
	// here rather than just the four canonical edge deltas.
	private static readonly (int DeltaColumn, int DeltaRow, Direction Direction, float UnitX, float UnitY)[] _neighborDeltas = [
		( 0, -1, Direction.North, 0f, -1f ),
		( 1, -1, Direction.NorthEast, 0.7071068f, -0.7071068f ),
		( 1, 0, Direction.East, 1f, 0f ),
		( 1, 1, Direction.SouthEast, 0.7071068f, 0.7071068f ),
		( 0, 1, Direction.South, 0f, 1f ),
		( -1, 1, Direction.SouthWest, -0.7071068f, 0.7071068f ),
		( -1, 0, Direction.West, -1f, 0f ),
		( -1, -1, Direction.NorthWest, -0.7071068f, -0.7071068f ),
	];

	private readonly IGridPressure _gridPressure;

	public GridProjection(
		IGridPressure gridPressure
	) {
		_gridPressure = gridPressure;
	}

	void IGridProjection.Update<TCell, TPressure, TProjectionStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> source,
		IMutableGrid<Velocity> destination,
		IMutableGrid<TPressure> pressureSource,
		IMutableGrid<TPressure> pressureDestination,
		TProjectionStrategy projection
	) {
		if( source.Width != destination.Width
			|| source.Height != destination.Height
			|| source.Width != pressureSource.Width
			|| source.Height != pressureSource.Height
			|| source.Width != pressureDestination.Width
			|| source.Height != pressureDestination.Height
		) {
			throw new ArgumentException( "Source, destination, and pressure grids must all have the same dimensions." );
		}

		int left = source.Column;
		int top = source.Row;
		int width = source.Width;
		int height = source.Height;
		if( width * height == 0 ) {
			return;
		}

		// Seed the pressure-correction field with the divergence of the source velocity
		// field, using the caller-supplied connectivity as the source of truth for which
		// neighbors participate in the discrete divergence operator at each cell.
		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				float divergence = CalculateDivergence( connectivity, source, column, row, left, top, width, height );
				GridCell<TPressure> cell = new( column, row, pressureSource[column, row] );
				pressureSource[column, row] = projection.SetDivergence( cell, divergence );
			}
		}

		// Iteratively relax the pressure-correction field towards convergence by
		// composing IGridPressure, double-buffering between pressureSource and
		// pressureDestination the same way callers of IGridPressure/IGridDiffusion do.
		IMutableGrid<TPressure> relaxationSource = pressureSource;
		IMutableGrid<TPressure> relaxationDestination = pressureDestination;
		for( int i = 0; i < Iterations; i++ ) {
			_gridPressure.Update<TCell, TPressure, float, TProjectionStrategy>(
				connectivity,
				relaxationSource,
				relaxationDestination,
				projection
			);

			( relaxationSource, relaxationDestination ) = ( relaxationDestination, relaxationSource );
		}

		// Subtract the gradient of the converged pressure-correction field from the
		// source velocity field, yielding a divergence-free result.
		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				Velocity gradient = CalculateGradient( connectivity, relaxationSource, projection, column, row, left, top, width, height );
				destination[column, row] = source[column, row] - gradient;
			}
		}
	}

	private static float CalculateDivergence<TCell>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> velocity,
		int column,
		int row,
		int left,
		int top,
		int width,
		int height
	) {
		float divergence = 0f;
		int neighborCount = 0;
		Velocity sourceVelocity = velocity[column, row];
		Direction sourceConnectivity = connectivity[column, row];

		foreach( (int deltaColumn, int deltaRow, Direction direction, float unitX, float unitY) in _neighborDeltas ) {
			if( TryGetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow ) ) {
				Velocity neighborVelocity = velocity[neighborColumn, neighborRow];
				divergence += ( ( neighborVelocity.X - sourceVelocity.X ) * unitX ) + ( ( neighborVelocity.Y - sourceVelocity.Y ) * unitY );
				neighborCount++;
			}
		}

		if( neighborCount == 0 ) {
			return 0f;
		}

		return divergence / neighborCount;
	}

	private static Velocity CalculateGradient<TCell, TPressure, TProjectionStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TPressure> pressure,
		TProjectionStrategy projection,
		int column,
		int row,
		int left,
		int top,
		int width,
		int height
	)
		where TProjectionStrategy : IProjectionStrategy<TPressure> {
		float gradientX = 0f;
		float gradientY = 0f;
		int neighborCount = 0;
		float sourcePressure = projection.GetPressure( pressure[column, row]! );
		Direction sourceConnectivity = connectivity[column, row];

		foreach( (int deltaColumn, int deltaRow, Direction direction, float unitX, float unitY) in _neighborDeltas ) {
			if( TryGetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow ) ) {
				float neighborPressure = projection.GetPressure( pressure[neighborColumn, neighborRow]! );
				float delta = neighborPressure - sourcePressure;
				gradientX += delta * unitX;
				gradientY += delta * unitY;
				neighborCount++;
			}
		}

		if( neighborCount == 0 ) {
			return Velocity.Zero;
		}

		return new Velocity( gradientX / neighborCount, gradientY / neighborCount );
	}

	private static bool TryGetNeighbor(
		Direction sourceConnectivity,
		Direction direction,
		int column,
		int row,
		int deltaColumn,
		int deltaRow,
		int left,
		int top,
		int width,
		int height,
		out int neighborColumn,
		out int neighborRow
	) {
		neighborColumn = column + deltaColumn;
		neighborRow = row + deltaRow;

		if( !sourceConnectivity.HasFlag( direction ) ) {
			return false;
		}

		if( neighborColumn < left
			|| neighborColumn >= left + width
			|| neighborRow < top
			|| neighborRow >= top + height
		) {
			return false;
		}

		return true;
	}

}
