namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

public interface IAtmosphericThermalsSettings {

	// Simulated seconds between heat exchanges between the gas and the structure.
	// Rounded to the nearest fixed tick (midpoints to even), with a minimum of one tick.
	// Must be finite and quantize to a representable number of ticks.
	float ExchangeInterval { get; }

}
