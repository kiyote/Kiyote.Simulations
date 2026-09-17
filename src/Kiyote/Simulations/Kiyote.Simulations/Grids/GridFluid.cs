using System.Buffers;
using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// A simplified Stable-Fluids style airflow engine: diffuse, project, advect, project.
// Scoped for spaceship-atmosphere style simulations - no buoyancy, temperature, or
// reaction modelling, and pressure is not renormalized between steps.
public sealed class GridFluid : IGridFluid {

	private static readonly (int DeltaColumn, int DeltaRow)[] _edgeDeltas = [
		( 1, 0 ),
		( 0, 1 ),
		( 1, 1 ),
		( 1, -1 ),
	];

	private const int ProjectionIterations = 20;

	// Reused across StepVelocity calls instead of renting/returning from ArrayPool.Shared
	// on every call - GridFluid is meant to be reused against a stable grid size, so these
	// only ever grow (never shrink) and typically allocate once for the life of the instance.
	private Velocity[]? _diffuseDeltas;
	private int[]? _diffuseNeighborCounts;
	private double[]? _projectDivergence;
	private double[]? _projectPressure;
	private Velocity[]? _advectBuffer;

	private static void EnsureCapacity<T>(
		ref T[]? buffer,
		int size
	) {
		if( buffer is null || buffer.Length < size ) {
			buffer = new T[size];
		}
	}

	void IGridFluid.StepVelocity<TCell, TVelocityAccessor, TVelocityDiffusion, TSampler, TBoundary, TCellSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		TVelocityDiffusion velocityDiffusion,
		TSampler sampler,
		TBoundary boundary,
		TCellSetCell setCell,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );

		VelocityGridView<TCell, TVelocityAccessor> velocity = new( grid, velocityAccessor );
		VelocityCallbackAdapter<TCell, TVelocityAccessor, TCellSetCell> velocitySetCell = new( grid, velocityAccessor, setCell );

		Diffuse( velocity, velocityDiffusion, boundary, velocitySetCell );
		Project( velocity, boundary, velocitySetCell );
		Advect( velocity, velocity, sampler, boundary, velocitySetCell, timeStep );
		Project( velocity, boundary, velocitySetCell );
	}

	void IGridFluid.AdvectGases<TCell, TVelocityAccessor, TScalar, TSampler, TPassability, TScalarSetCell>(
		IGrid<TCell> grid,
		TVelocityAccessor velocityAccessor,
		IReadOnlyList<GasSpecies<TScalar, TScalarSetCell>> gases,
		TSampler sampler,
		TPassability passability,
		double timeStep
	) {
		ArgumentNullException.ThrowIfNull( grid );
		ArgumentNullException.ThrowIfNull( gases );

		VelocityGridView<TCell, TVelocityAccessor> velocity = new( grid, velocityAccessor );

		for( int i = 0; i < gases.Count; i++ ) {
			GasSpecies<TScalar, TScalarSetCell> gas = gases[i];
			AdvectScalar( velocity, gas.Concentration, sampler, passability, gas.SetCell, timeStep );
		}
	}

	private void Diffuse<TVelocityGrid, TVelocityDiffusion, TBoundary, TVelocitySetCell>(
		TVelocityGrid velocity,
		TVelocityDiffusion velocityDiffusion,
		TBoundary boundary,
		TVelocitySetCell velocitySetCell
	)
		where TVelocityGrid : IGrid<Velocity>
		where TVelocityDiffusion : IDiffusionStrategy<Velocity, Velocity>
		where TBoundary : IBoundaryStrategy
		where TVelocitySetCell : ICallbackStrategy<Velocity, Velocity> {

		int left = velocity.Column;
		int top = velocity.Row;
		int width = velocity.Width;
		int height = velocity.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		EnsureCapacity( ref _diffuseDeltas, size );
		EnsureCapacity( ref _diffuseNeighborCounts, size );
		Velocity[] deltas = _diffuseDeltas!;
		int[] neighborCounts = _diffuseNeighborCounts!;
		{
			Array.Clear( deltas, 0, size );
			Array.Clear( neighborCounts, 0, size );

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int sourceIndex = column - left + ( ( row - top ) * width );

					foreach( (int deltaColumn, int deltaRow) in _edgeDeltas ) {
						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
							|| boundary.IsSolid( neighborColumn, neighborRow )
						) {
							continue;
						}

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );
						neighborCounts[sourceIndex]++;
						neighborCounts[destinationIndex]++;
					}
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int sourceIndex = column - left + ( ( row - top ) * width );
					GridCell<Velocity> source = new( column, row, velocity[column, row] );

					foreach( (int deltaColumn, int deltaRow) in _edgeDeltas ) {
						int neighborColumn = column + deltaColumn;
						int neighborRow = row + deltaRow;

						if( neighborColumn < left
							|| neighborColumn >= left + width
							|| neighborRow < top
							|| neighborRow >= top + height
							|| boundary.IsSolid( neighborColumn, neighborRow )
						) {
							continue;
						}

						int destinationIndex = neighborColumn - left + ( ( neighborRow - top ) * width );
						GridCell<Velocity> destination = new( neighborColumn, neighborRow, velocity[neighborColumn, neighborRow] );

						Velocity transfer = velocityDiffusion.CalculateTransfer( source, destination, neighborCounts[sourceIndex], neighborCounts[destinationIndex] );

						deltas[sourceIndex] = velocityDiffusion.Combine( deltas[sourceIndex], velocityDiffusion.Negate( transfer ) );
						deltas[destinationIndex] = velocityDiffusion.Combine( deltas[destinationIndex], transfer );
					}
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int index = column - left + ( ( row - top ) * width );
					GridCell<Velocity> cell = new( column, row, velocity[column, row] );
					Velocity updated = velocityDiffusion.Apply( cell, deltas[index] );
					velocitySetCell.Callback( cell, updated );
				}
			}
		}
	}

	// Iterative Gauss-Seidel relaxation solving for a divergence-free velocity field.
	private void Project<TVelocityGrid, TBoundary, TVelocitySetCell>(
		TVelocityGrid velocity,
		TBoundary boundary,
		TVelocitySetCell velocitySetCell
	)
		where TVelocityGrid : IGrid<Velocity>
		where TBoundary : IBoundaryStrategy
		where TVelocitySetCell : ICallbackStrategy<Velocity, Velocity> {

		int left = velocity.Column;
		int top = velocity.Row;
		int width = velocity.Width;
		int height = velocity.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		EnsureCapacity( ref _projectDivergence, size );
		EnsureCapacity( ref _projectPressure, size );
		double[] divergence = _projectDivergence!;
		double[] pressure = _projectPressure!;
		{
			Array.Clear( divergence, 0, size );
			Array.Clear( pressure, 0, size );

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					int index = column - left + ( ( row - top ) * width );

					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					Velocity own = velocity[column, row];
					Velocity right = SampleOrZero( velocity, boundary, column + 1, row, own );
					Velocity leftV = SampleOrZero( velocity, boundary, column - 1, row, own );
					Velocity down = SampleOrZero( velocity, boundary, column, row + 1, own );
					Velocity up = SampleOrZero( velocity, boundary, column, row - 1, own );

					divergence[index] = 0.5 * (  right.X - leftV.X  + ( down.Y - up.Y ) );
				}
			}

			for( int iteration = 0; iteration < ProjectionIterations; iteration++ ) {
				for( int row = top; row < top + height; row++ ) {
					for( int column = left; column < left + width; column++ ) {
						if( boundary.IsSolid( column, row ) ) {
							continue;
						}

						int index = column - left + ( ( row - top ) * width );

						double right = SamplePressureOrZero( pressure, boundary, left, top, width, height, column + 1, row );
						double leftP = SamplePressureOrZero( pressure, boundary, left, top, width, height, column - 1, row );
						double down = SamplePressureOrZero( pressure, boundary, left, top, width, height, column, row + 1 );
						double up = SamplePressureOrZero( pressure, boundary, left, top, width, height, column, row - 1 );

						pressure[index] = ( (divergence[index] * -1.0) + right + leftP + down + up ) * 0.25;
					}
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					double right = SamplePressureOrZero( pressure, boundary, left, top, width, height, column + 1, row );
					double leftP = SamplePressureOrZero( pressure, boundary, left, top, width, height, column - 1, row );
					double down = SamplePressureOrZero( pressure, boundary, left, top, width, height, column, row + 1 );
					double up = SamplePressureOrZero( pressure, boundary, left, top, width, height, column, row - 1 );

					GridCell<Velocity> cell = new( column, row, velocity[column, row] );
					Velocity gradient = new( 0.5 * ( right - leftP ), 0.5 * ( down - up ) );
					Velocity updated = cell.Cell - gradient;
					velocitySetCell.Callback( cell, updated );
				}
			}
		}
	}

	// Samples the velocity at column,row for use by a fluid cell at (own). Cells
	// outside the grid have no boundary to reflect off of and remain zero, but a
	// solid neighbor reflects the requesting cell's own velocity back per the
	// boundary strategy, rather than simply vanishing it - a genuine collision
	// response instead of an absorbing wall.
	private static Velocity SampleOrZero<TVelocityGrid, TBoundary>(
		TVelocityGrid velocity,
		TBoundary boundary,
		int column,
		int row,
		Velocity own
	)
		where TVelocityGrid : IGrid<Velocity>
		where TBoundary : IBoundaryStrategy {

		if( column < velocity.Column
			|| column >= velocity.Column + velocity.Width
			|| row < velocity.Row
			|| row >= velocity.Row + velocity.Height
		) {
			return Velocity.Zero;
		}

		if( boundary.IsSolid( column, row ) ) {
			return boundary.ReflectVelocity( own, column, row );
		}

		return velocity[column, row];
	}

	private static double SamplePressureOrZero<TBoundary>(
		double[] pressure,
		TBoundary boundary,
		int left,
		int top,
		int width,
		int height,
		int column,
		int row
	)
		where TBoundary : IBoundaryStrategy {

		if( column < left
			|| column >= left + width
			|| row < top
			|| row >= top + height
			|| boundary.IsSolid( column, row )
		) {
			return 0.0;
		}

		int index = column - left + ( ( row - top ) * width );
		return pressure[index];
	}

	private void Advect<TVelocityGrid, TSampler, TBoundary, TVelocitySetCell>(
		TVelocityGrid source,
		TVelocityGrid velocity,
		TSampler sampler,
		TBoundary boundary,
		TVelocitySetCell velocitySetCell,
		double timeStep
	)
		where TVelocityGrid : IGrid<Velocity>
		where TSampler : IGridSampler<double, Velocity>
		where TBoundary : IBoundaryStrategy
		where TVelocitySetCell : ICallbackStrategy<Velocity, Velocity> {

		int left = velocity.Column;
		int top = velocity.Row;
		int width = velocity.Width;
		int height = velocity.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		EnsureCapacity( ref _advectBuffer, size );
		Velocity[] advected = _advectBuffer!;
		{
			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					int index = column - left + ( ( row - top ) * width );

					if( boundary.IsSolid( column, row ) ) {
						advected[index] = Velocity.Zero;
						continue;
					}

					Velocity current = velocity[column, row];
					double traceColumn = column - ( current.X * timeStep );
					double traceRow = row - ( current.Y * timeStep );

					// Uses IGridSampler's generic Sample<TGrid> overload so source (a struct
					// implementing IGrid<Velocity>) is sampled without boxing.
					advected[index] = sampler.Sample( source, traceColumn, traceRow );
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( boundary.IsSolid( column, row ) ) {
						continue;
					}

					int index = column - left + ( ( row - top ) * width );
					GridCell<Velocity> cell = new( column, row, velocity[column, row] );
					velocitySetCell.Callback( cell, advected[index] );
				}
			}
		}
	}

	private static void AdvectScalar<TVelocityGrid, TScalar, TSampler, TPassability, TScalarSetCell>(
		TVelocityGrid velocity,
		IGrid<TScalar> concentration,
		TSampler sampler,
		TPassability passability,
		TScalarSetCell scalarSetCell,
		double timeStep
	)
		where TVelocityGrid : IGrid<Velocity>
		where TSampler : IGridSampler<double, TScalar>
		where TPassability : IGridPassability
		where TScalarSetCell : ICallbackStrategy<TScalar, TScalar> {

		int left = concentration.Column;
		int top = concentration.Row;
		int width = concentration.Width;
		int height = concentration.Height;
		int size = width * height;

		if( size == 0 ) {
			return;
		}

		TScalar[] advected = ArrayPool<TScalar>.Shared.Rent( size );
		try {
			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					int index = column - left + ( ( row - top ) * width );

					if( passability.IsSolid( column, row ) ) {
						advected[index] = default!;
						continue;
					}

					Velocity current = velocity[column, row];
					double traceColumn = column - ( current.X * timeStep );
					double traceRow = row - ( current.Y * timeStep );

					advected[index] = sampler.Sample( concentration, traceColumn, traceRow );
				}
			}

			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( passability.IsSolid( column, row ) ) {
						continue;
					}

					int index = column - left + ( ( row - top ) * width );
					GridCell<TScalar> cell = new( column, row, concentration[column, row] );
					scalarSetCell.Callback( cell, advected[index] );
				}
			}
		} finally {
			ArrayPool<TScalar>.Shared.Return( advected );
		}
	}

}
