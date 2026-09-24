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

	void IGridPressure.Update<TCell, TPressure, TFlow, TPressureStrategy>(
		IConnectivityGrid<TCell> connectivity,
		IGrid<TPressure> source,
		IMutableGrid<TPressure> destination,
		TPressureStrategy pressure
	) {
		ArgumentNullException.ThrowIfNull( pressure );

		_gridDiffusion.Update<TCell, TPressure, TFlow, PressureDiffusionStrategy<TPressure, TFlow, TPressureStrategy>>(
			connectivity,
			source,
			destination,
			new PressureDiffusionStrategy<TPressure, TFlow, TPressureStrategy>( pressure, _clock.FixedTimeStep )
		);
	}

	// Adapts an IPressureStrategy, along with a fixed deltaTime, into the shape that
	// GridDiffusion's flow algorithm expects, letting GridPressure reuse the diffusion
	// simulation's neighbor accumulation/apply logic instead of duplicating it.
	private readonly struct PressureDiffusionStrategy<TPressure, TFlow, TPressureStrategy> : IDiffusionStrategy<TPressure, TFlow>
		where TPressureStrategy : IPressureStrategy<TPressure, TFlow> {

		private readonly TPressureStrategy _pressure;
		private readonly float _deltaTime;

		public PressureDiffusionStrategy(
			TPressureStrategy pressure,
			float deltaTime
		) {
			_pressure = pressure;
			_deltaTime = deltaTime;
		}

		TFlow IDiffusionStrategy<TPressure, TFlow>.CalculateTransfer(
			GridCell<TPressure> source,
			GridCell<TPressure> destination,
			int sourceNeighborCount,
			int destinationNeighborCount
		) {
			return _pressure.CalculateFlow( source, destination, sourceNeighborCount, destinationNeighborCount, _deltaTime );
		}

		TFlow IDiffusionStrategy<TPressure, TFlow>.Combine(
			TFlow left,
			TFlow right
		) {
			return _pressure.Combine( left, right );
		}

		TFlow IDiffusionStrategy<TPressure, TFlow>.Negate(
			TFlow value
		) {
			return _pressure.Negate( value );
		}

		TPressure IDiffusionStrategy<TPressure, TFlow>.Apply(
			GridCell<TPressure> cell,
			TFlow delta
		) {
			return _pressure.Apply( cell, delta );
		}

	}

}
