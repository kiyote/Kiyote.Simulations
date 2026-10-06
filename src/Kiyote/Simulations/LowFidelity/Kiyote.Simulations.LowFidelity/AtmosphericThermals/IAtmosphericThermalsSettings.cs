namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

public interface IAtmosphericThermalsSettings {

	// Simulated seconds between heat exchanges between the gas and the structure.
	// Time is counted in simulation clock ticks; an exchange runs once enough ticks have elapsed.
	float ExchangeInterval { get; }

}
