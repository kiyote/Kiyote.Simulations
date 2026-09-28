using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Projection;

public sealed class GridProjection : IGridProjection {

	// Number of Jacobi relaxation iterations performed against the pressure-correction
	// field before it is considered converged enough to subtract its gradient from the
	// source velocity field.
	private const int Iterations = 20;

	// The divergence and gradient operators below average (neighbor - self) projected
	// onto the 8 unit directions and divide by 8, so for smooth fields each behaves
	// like s * (true operator) with s = (2 + 2*sqrt(2)) / 8. Their composition is
	// therefore ~s^2 * Laplacian, while the compact 8-neighbor graph Laplacian used by
	// the Jacobi solve is ~3 * Laplacian. Scaling the divergence right-hand side by
	// 3 / s^2 makes subtracting the gradient of the solved field cancel divergence.
	private const float PoissonScale = 8.235294f;

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

	public GridProjection() {
	}

	void IGridProjection.Update<TCell, TPressure, TProjectionStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> source,
		IMutableGrid<Velocity> destination,
		IMutableGrid<TPressure> pressureSource,
		IMutableGrid<TPressure> pressureDestination,
		IMutableGrid<float> divergence,
		TProjectionStrategy projection
	) {
		if( source.Width != destination.Width
			|| source.Height != destination.Height
			|| source.Width != pressureSource.Width
			|| source.Height != pressureSource.Height
			|| source.Width != pressureDestination.Width
			|| source.Height != pressureDestination.Height
			|| source.Width != divergence.Width
			|| source.Height != divergence.Height
		) {
			throw new ArgumentException( "Source, destination, pressure, and divergence grids must all have the same dimensions." );
		}

		int left = source.Column;
		int top = source.Row;
		int width = source.Width;
		int height = source.Height;
		if( width * height == 0 ) {
			return;
		}

		// Compute the divergence of the source velocity field once; it is the fixed
		// right-hand side of the Poisson equation Laplacian(p) = div(v). (Previously it
		// was written into the pressure field and then diffused, which yields
		// p ~ blur(div v) rather than solving for p; subtracting grad(blur(div v))
		// amplifies divergence every call and made the velocity field blow up.)
		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				if( connectivity[column, row] == Direction.None ) {
					divergence[column, row] = 0f;
					continue;
				}
				divergence[column, row] = PoissonScale * CalculateDivergence( connectivity, source, column, row, left, top, width, height );
			}
		}

		// Jacobi iterations of the Poisson equation, warm-started from whatever is
		// already in pressureSource (typically the previous call's solution). Blocked
		// edges are omitted, which is a zero-gradient (Neumann) boundary.
		IMutableGrid<TPressure> relaxationSource = pressureSource;
		IMutableGrid<TPressure> relaxationDestination = pressureDestination;
		for( int i = 0; i < Iterations; i++ ) {
			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					TPressure current = relaxationSource[column, row]!;
					Direction sourceConnectivity = connectivity[column, row];
					float neighborSum = 0f;
					int neighborCount = 0;
					if( sourceConnectivity != Direction.None ) {
						foreach( (int deltaColumn, int deltaRow, Direction direction, float _, float _) in _neighborDeltas ) {
							NeighborKind kind = GetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow );
							if( kind == NeighborKind.Cell ) {
								neighborSum += projection.GetPressure( relaxationSource[neighborColumn, neighborRow]! );
								neighborCount++;
							} else if( kind == NeighborKind.Outside ) {
								// Open edge to nothingness: fixed zero (Dirichlet) pressure.
								neighborCount++;
							}
						}
					}

					if( neighborCount == 0 ) {
						relaxationDestination[column, row] = current;
						continue;
					}

					float updated = ( neighborSum - divergence[column, row] ) / neighborCount;
					float delta = updated - projection.GetPressure( current );
					relaxationDestination[column, row] = projection.Apply( new GridCell<TPressure>( column, row, current ), delta );
				}
			}

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
				velocity, sourceConnectivity, sourceVelocity, direction,
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
	private static Velocity GetNeighborOrReflectedVelocity(
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
		NeighborKind kind = GetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow );
		if( kind == NeighborKind.Cell ) {
			return velocity[neighborColumn, neighborRow];
		}
		if( kind == NeighborKind.Outside ) {
			// Open edge: zero-gradient outflow, the flow simply leaves the domain.
			return sourceVelocity;
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
			NeighborKind kind = GetNeighbor( sourceConnectivity, direction, column, row, deltaColumn, deltaRow, left, top, width, height, out int neighborColumn, out int neighborRow );
			if( kind == NeighborKind.Cell ) {
				neighborPressure = projection.GetPressure( pressure[neighborColumn, neighborRow]! );
			} else if( kind == NeighborKind.Outside ) {
				neighborPressure = 0f;
			}

			float delta = neighborPressure - sourcePressure;
			gradientX += delta * unitX;
			gradientY += delta * unitY;
		}

		return new Velocity( gradientX / _neighborDeltas.Length, gradientY / _neighborDeltas.Length );
	}


	private enum NeighborKind {
		Wall,
		Cell,
		Outside
	}

	// An unflagged edge is a hard wall that reflects. A flagged edge that leads off the
	// grid is open to nothingness: pressure and flow pass out and are lost.
	private static NeighborKind GetNeighbor(
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
			return NeighborKind.Wall;
		}

		if( neighborColumn < left
			|| neighborColumn >= left + width
			|| neighborRow < top
			|| neighborRow >= top + height
		) {
			return NeighborKind.Outside;
		}

		return NeighborKind.Cell;
	}

}
