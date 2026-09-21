using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Diffusion;

namespace Kiyote.Simulations.Temperature;

public sealed class GridTemperature : IGridTemperature {

	private readonly IGridDiffusion _gridDiffusion;
	private readonly IGridRadiation _gridRadiation;

	public GridTemperature(
		IGridDiffusion gridDiffusion,
		IGridRadiation gridRadiation
	) {
		_gridDiffusion = gridDiffusion;
		_gridRadiation = gridRadiation;
	}

	void IGridTemperature.Update<TCell, TFlow, TDiffusionStrategy, TPassability, TExposure, TRadiationStrategy, TSetCell>(
		IGrid<TCell> grid,
		TDiffusionStrategy diffusionStrategy,
		TPassability isPassable,
		TExposure isExposed,
		TRadiationStrategy radiationStrategy,
		TSetCell setCell,
		double timeStep
	) {
		// Conduction first, spreading heat internally, then radiation carries
		// the exposed edges' heat away into space.
		_gridDiffusion.Update<TCell, TFlow, TDiffusionStrategy, TPassability, TSetCell>(
			grid,
			diffusionStrategy,
			isPassable,
			setCell,
			timeStep
		);

		_gridRadiation.Update<TCell, TExposure, TRadiationStrategy, TSetCell>(
			grid,
			isExposed,
			radiationStrategy,
			setCell,
			timeStep
		);
	}

}
