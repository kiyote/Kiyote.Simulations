namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public sealed record GasDefinition(
	string Id,
	string Name,
	float? CondensationPoint,
	float? FreezingPoint
);
