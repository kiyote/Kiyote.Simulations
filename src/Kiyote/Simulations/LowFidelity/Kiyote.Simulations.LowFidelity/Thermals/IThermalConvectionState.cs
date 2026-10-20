using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

// Trusted addresses and capacities belong to the current, prepared coupled compilation.
internal interface IThermalConvectionState {
	IGridLayer<float> Temperature { get; }
	void AddEnergyAt( int chunk, int local, float capacity, float joules );
}
