namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

// One structure face touching a gas cell. Conductance (W/K) and Capacity (J/K) come from its material.
internal readonly record struct ConvectionLink(
	int Chunk,
	int Local,
	float Conductance,
	float Capacity
);
