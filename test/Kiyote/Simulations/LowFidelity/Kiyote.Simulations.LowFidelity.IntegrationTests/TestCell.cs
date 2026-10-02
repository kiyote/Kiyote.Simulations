namespace Kiyote.Simulations.LowFidelity.IntegrationTests;

public readonly record struct TestCell(
	bool IsWalkable,
	bool IsGasPermeable,
	bool IsVacuum
);
