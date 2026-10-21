namespace Kiyote.Simulations.LowFidelity.Visualizer.Thermals;

internal sealed class ThermalVisualizerClock : ISimulationClock {

	float ISimulationClock.FixedTimeStep => 10.0f;

}
