using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridAirflow;

// Bridges IGridPressure.Inject's ICellStrategy<TCell,bool>-shaped passability
// check to an IBoundaryStrategy's IGridPassability.IsSolid check, so the same
// AirflowBoundaryStrategy instance can drive both the velocity/gas-advection
// side (IGridFluid/IGridAirflow) and the pump-injection side (IGridPressure).
[ExcludeFromCodeCoverage]
internal readonly struct AirflowPassabilityAdapter : ICellStrategy<double, bool> {

	private readonly AirflowBoundaryStrategy _boundary;

	public AirflowPassabilityAdapter(
		AirflowBoundaryStrategy boundary
	) {
		_boundary = boundary;
	}

	public bool Evaluate(
		GridCell<double> cell
	) {
		return !_boundary.IsSolid( cell.Column, cell.Row );
	}

}
