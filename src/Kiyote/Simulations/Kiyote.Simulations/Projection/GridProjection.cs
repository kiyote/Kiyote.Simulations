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
				if( connectivity[column, row] == Direction.None ) {
					continue;
				}
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
				if( connectivity[column, row] == Direction.None ) {
					destination[column, row] = source[column, row];
					continue;
				}
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
		Velocity sourceVelocity = velocity[column, row];
		Direction sourceConnectivity = connectivity[column, row];

		foreach( (int deltaColumn, int deltaRow, Direction direction, float unitX, float unitY) in _neighborDeltas ) {
			Velocity neighborVelocity = GetNeighborOrReflectedVelocity(
				connectivity, velocity, sourceConnectivity, sourceVelocity, direction,
				column, row, deltaColumn, deltaRow, unitX, unitY, left, top, width, height
			);
			divergence += ( ( neighborVelocity.X - sourceVelocity.X ) * unitX ) + ( ( neighborVelocity.Y - sourceVelocity.Y ) * unitY );
		}

		return divergence / _neighborDeltas.Length;
	}

	// Returns the neighbor's actual velocity when the edge is open, or, when the edge is
	// blocked (either by an impassable neighbor or the boundary of the domain), a reflected
	// "ghost" velocity: the source cell's own velocity with its component normal to the wall
	// negated (no-penetration) while its tangential component is preserved (free-slip). This
	// treats every wall/edge uniformly as a solid boundary rather than simply omitting it from
	// the divergence average.
	private static Velocity GetNeighborOrReflectedVelocity<TCell>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> velocity,
		Direction sourceConnectivity,
		Velocity sourceVelocity,
		Direction direction,
		int column,
		int row,
		int deltaColumn,
		int deltaRow,
		float unitX,
		float unitY,
		int left,
		int top,
		int width,
		int height
	) {
		if( TryGetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow ) ) {
			return velocity[neighborColumn, neighborRow];
		}

		float normalComponent = ( sourceVelocity.X * unitX ) + ( sourceVelocity.Y * unitY );
		return new Velocity(
			sourceVelocity.X - ( 2f * normalComponent * unitX ),
			sourceVelocity.Y - ( 2f * normalComponent * unitY )
		);
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
		float sourcePressure = projection.GetPressure( pressure[column, row]! );
		Direction sourceConnectivity = connectivity[column, row];

		foreach( (int deltaColumn, int deltaRow, Direction direction, float unitX, float unitY) in _neighborDeltas ) {
			// A blocked edge (wall or domain boundary) is treated as a zero-gradient (Neumann)
			// boundary: the "ghost" pressure across the wall is reflected to equal the source
			// cell's own pressure, contributing no gradient in that direction rather than being
			// omitted from the average.
			float neighborPressure = sourcePressure;
			if( TryGetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow ) ) {
				neighborPressure = projection.GetPressure( pressure[neighborColumn, neighborRow]! );
			}

			float delta = neighborPressure - sourcePressure;
			gradientX += delta * unitX;
			gradientY += delta * unitY;
		}

		return new Velocity( gradientX / _neighborDeltas.Length, gradientY / _neighborDeltas.Length );
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
