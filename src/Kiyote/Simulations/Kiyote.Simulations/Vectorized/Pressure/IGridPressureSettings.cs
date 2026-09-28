namespace Kiyote.Simulations.Vectorized.Pressure;

public interface IGridPressureSettings {

	// Equalisation rate per unit of simulated time. Rate * FixedTimeStep must not
	// exceed 1/8 to remain stable.
	float Rate { get; }

}
