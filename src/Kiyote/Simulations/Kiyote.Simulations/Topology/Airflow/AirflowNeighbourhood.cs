using Kiyote.Geometry.Topology;
using Kiyote.Simulations.Topology.Advection;
using Kiyote.Simulations.Topology.Projection;

namespace Kiyote.Simulations.Topology.Airflow;

// Caller-owned neighbour description used by GridAirflow, built once per topology by
// IGridAirflow.CreateNeighbourhood. Bundles the neighbourhoods required by the layered
// simulations; Pressure is the combined connectivity | vacuum layer shared with projection.
public sealed class AirflowNeighbourhood {

	public AirflowNeighbourhood(
		IGridLayer<Direction> pressure,
		ProjectionNeighbourhood projection,
		AdvectionNeighbourhood advection
	) {
		Pressure = pressure;
		Projection = projection;
		Advection = advection;
	}

	public IGridLayer<Direction> Pressure { get; }

	public ProjectionNeighbourhood Projection { get; }

	public AdvectionNeighbourhood Advection { get; }

}
