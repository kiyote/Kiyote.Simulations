namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

// A gas cell and the range of structure faces it exchanges heat with.
internal readonly record struct ConvectionCell(
	int Chunk,
	int Local,
	int FirstLink,
	int LinkCount
);
