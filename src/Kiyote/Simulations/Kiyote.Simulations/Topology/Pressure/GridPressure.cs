using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Diffusion;

namespace Kiyote.Simulations.Topology.Pressure;

// Pressure equalisation is diffusion scaled by the simulation's fixed time step, so this
// reuses the Topology GridDiffusion with an effective per-step rate of Rate * dt.
public sealed class GridPressure : IGridPressure {

	private readonly IGridDiffusion _diffusion;

	public GridPressure(
		IGridPressureSettings settings,
		ISimulationClock clock
	) {
		_diffusion = new GridDiffusion( new StepSettings( settings.Rate * clock.FixedTimeStep ) );
	}

	IGridLayer<Direction> IGridPressure.CreateNeighbourhood<TCell>(
		ICompiledGridAssembly<TCell> compiled,
		IGridLayer<Direction> connectivity
	) {
		return _diffusion.CreateNeighbourhood( compiled, connectivity );
	}

	void IGridPressure.Update(
		IGridLayer<Direction> neighbourhood,
		IGridLayer<float> source,
		IGridLayer<float> destination
	) {
		_diffusion.Update( neighbourhood, source, destination );
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
