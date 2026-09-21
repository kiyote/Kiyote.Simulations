namespace Kiyote.Simulations;

public sealed record MaterialProperties(
	float Density,
	float ThermalConductivity,
	float SpecificHeatCapacity,
	float MeltingPoint,
	float BoilingPoint
);
