using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Temperature;

public interface IRadiativeLossStrategy<TCell> {

	TCell Apply(
		GridCell<TCell> cell,
		double timeStep
	);

}
