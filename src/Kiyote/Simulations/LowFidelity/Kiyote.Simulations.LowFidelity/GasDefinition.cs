namespace Kiyote.Simulations.LowFidelity;

public sealed record GasDefinition(
	string Id,
	string Name,
	float? CondensationPoint,
	float? FreezingPoint
);
