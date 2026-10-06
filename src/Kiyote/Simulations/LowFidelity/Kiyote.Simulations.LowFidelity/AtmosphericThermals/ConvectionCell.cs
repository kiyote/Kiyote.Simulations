namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

// A gas cell and the range of structure faces it exchanges heat with.
internal readonly record struct ConvectionCell(
	int Column,
	int Row,
	int FirstLink,
	int LinkCount
);

// One structure face touching a gas cell. Conductance (W/K) and Capacity (J/K) come from its material.
internal readonly record struct ConvectionLink(
	int Column,
	int Row,
	float Conductance,
	float Capacity
);
