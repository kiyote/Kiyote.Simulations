using Kiyote.Geometry.Grids;
using Kiyote.Geometry.Grids.Connectivity;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Pressure;

public sealed class GridPressure : IGridPressure {

	private readonly IGridDiffusion _gridDiffusion;
	private readonly ISimulationClock _clock;

	public GridPressure(
		IGridDiffusion gridDiffusion,
		ISimulationClock clock
	) {
		ArgumentNullException.ThrowIfNull( gridDiffusion );
		ArgumentNullException.ThrowIfNull( clock );

		_gridDiffusion = gridDiffusion;
		_clock = clock;
	}

	void IGridPressure.Update<TCell, TFlow, TPressureStrategy>(
		IGrid<TCell> source,
		IConnectivityGrid<TCell> connectivity,
		IMutableGrid<TCell> destination,
		TPressureStrategy pressure
	) {
		ArgumentNullException.ThrowIfNull( pressure );

		_gridDiffusion.Update<TCell, TFlow, PressureDiffusionStrategy<TCell, TFlow, TPressureStrategy>>(
			source,
			connectivity,
			destination,
			new PressureDiffusionStrategy<TCell, TFlow, TPressureStrategy>( pressure, _clock.FixedTimeStep )
		);
	}

	// Adapts an IPressureStrategy, along with a fixed deltaTime, into the shape that
	// GridDiffusion's flow algorithm expects, letting GridPressure reuse the diffusion
	// simulation's neighbor accumulation/apply logic instead of duplicating it.
	private readonly struct PressureDiffusionStrategy<TCell, TFlow, TPressureStrategy> : IDiffusionStrategy<TCell, TFlow>
		where TPressureStrategy : IPressureStrategy<TCell, TFlow> {

		private readonly TPressureStrategy _pressure;
		private readonly float _deltaTime;

		public PressureDiffusionStrategy(
			TPressureStrategy pressure,
			float deltaTime
		) {
			_pressure = pressure;
			_deltaTime = deltaTime;
		}

		TFlow IDiffusionStrategy<TCell, TFlow>.CalculateTransfer(
			GridCell<TCell> source,
			GridCell<TCell> destination,
			int sourceNeighborCount,
			int destinationNeighborCount
		) {
			return _pressure.CalculateFlow( source, destination, sourceNeighborCount, destinationNeighborCount, _deltaTime );
		}

		TFlow IDiffusionStrategy<TCell, TFlow>.Combine(
			TFlow left,
			TFlow right
		) {
			return _pressure.Combine( left, right );
		}

		TFlow IDiffusionStrategy<TCell, TFlow>.Negate(
			TFlow value
		) {
			return _pressure.Negate( value );
		}

		TCell IDiffusionStrategy<TCell, TFlow>.Apply(
			GridCell<TCell> cell,
			TFlow delta
		) {
			return _pressure.Apply( cell, delta );
		}

	}

}
