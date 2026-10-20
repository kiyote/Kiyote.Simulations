using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Trusted addresses and capacities belong to the current, prepared coupled compilation.
internal interface IAtmosphereConvectionState {
	IGridLayer<float> Temperature { get; }
	float GetTotalGasAt( int chunk, int local );
	float AddEnergyAt( int chunk, int local, float capacity, float joules );
}
