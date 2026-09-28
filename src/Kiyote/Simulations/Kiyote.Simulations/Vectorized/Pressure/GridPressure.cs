using Kiyote.Simulations.Vectorized.Diffusion;

namespace Kiyote.Simulations.Vectorized.Pressure;

// Pressure equalisation is diffusion scaled by the simulation's fixed time step, so this
// reuses the vectorized GridDiffusion with an effective per-step rate of Rate * dt.
public sealed class GridPressure : IGridPressure {

	private readonly IGridDiffusion _diffusion;

	public GridPressure(
		IGridPressureSettings settings,
		ISimulationClock clock
	) {
		_diffusion = new GridDiffusion( new StepSettings( settings.Rate * clock.FixedTimeStep ) );
	}

	void IGridPressure.Update<TCell>(
		Field<TCell> source,
		Field<TCell> destination
	) {
		_diffusion.Update( source, destination );
	}

	private sealed class StepSettings : IGridDiffusionSettings {

		public StepSettings(
			float rate
		) {
			Rate = rate;
		}

		public float Rate { get; }

	}

}
