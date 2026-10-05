namespace Kiyote.Simulations.LowFidelity;

// Conductivity: W/K across one shared face. HeatCapacity: J/K for one cell.
// Emissivity and Absorptivity are 0..1 fractions for radiation to space and sunlight.
public sealed record MaterialDefinition(
	string Id,
	string Name,
	float Conductivity,
	float HeatCapacity,
	float Emissivity,
	float Absorptivity
);
