namespace Kiyote.Simulations.LowFidelity.Thermals;

internal interface IThermalConduction {

	// Writes temperatureNext from temperature plus heat exchanged across faces shared with
	// occupied neighbours. Unoccupied cells have zero capacity and exchange nothing.
	// temperature, conductivity and capacity are padded spans; temperatureNext is a chunk span
	// (see AtmosphereSimd). Every cell of temperatureNext is written.
	void Conduct(
		ReadOnlySpan<ulong> presence,
		float dt,
		ReadOnlySpan<float> temperature,
		ReadOnlySpan<float> conductivity,
		ReadOnlySpan<float> capacity,
		Span<float> temperatureNext
	);

}
