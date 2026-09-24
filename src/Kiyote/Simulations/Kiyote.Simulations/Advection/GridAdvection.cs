using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;

namespace Kiyote.Simulations.Advection;

public sealed class GridAdvection : IGridAdvection {

	private readonly ISimulationClock _clock;

	public GridAdvection(
		ISimulationClock clock
	) {
		ArgumentNullException.ThrowIfNull( clock );

		_clock = clock;
	}

	void IGridAdvection.Update<TCell, TValue, TSampler>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<Velocity> velocity,
		IGrid<TValue> source,
		IMutableGrid<TValue> destination,
		TSampler sampler
	) {
		ArgumentNullException.ThrowIfNull( connectivity );
		ArgumentNullException.ThrowIfNull( velocity );
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( sampler );
		if( source.Width != destination.Width
			|| source.Height != destination.Height
			|| source.Width != velocity.Width
			|| source.Height != velocity.Height
		) {
			throw new ArgumentException( "Source, destination, and velocity grids must all have the same dimensions." );
		}

		int left = source.Column;
		int top = source.Row;
		int width = source.Width;
		int height = source.Height;
		if( width * height == 0 ) {
			return;
		}

		for( int row = top; row < top + height; row++ ) {
			for( int column = left; column < left + width; column++ ) {
				// Cells with no open connectivity have nowhere to receive a sample
				// from, so their value is left unchanged rather than advected.
				if( connectivity[column, row] == Direction.None ) {
					destination[column, row] = source[column, row];
					continue;
				}

				Velocity current = velocity[column, row];
				float traceColumn = column - ( current.X * _clock.FixedTimeStep );
				float traceRow = row - ( current.Y * _clock.FixedTimeStep );

				destination[column, row] = sampler.Sample( source, traceColumn, traceRow );
			}
		}
	}

}
