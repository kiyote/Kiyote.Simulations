namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IAtmosphericsSettings {

	// Flow acceleration per kPa of pressure difference, per second.
	float Acceleration { get; }

	// Fraction of face flow lost per second.
	float Friction { get; }

	// Fraction of temperature difference exchanged per second across open faces.
	float Conduction { get; }

	// Joules required to raise one unit of gas by one kelvin.
	float HeatCapacity { get; }

	// Fraction of gas (or condensate) changing phase per second.
	float CondensationRate { get; }

	// Seconds per internal step.
	float FixedTimeStep { get; }

	int MaxStepsPerAdvance { get; }

	float WindScale { get; }

}
