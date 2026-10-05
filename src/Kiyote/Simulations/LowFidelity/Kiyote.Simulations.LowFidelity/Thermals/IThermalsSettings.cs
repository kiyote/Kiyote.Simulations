namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IThermalsSettings {

	// Effective Stefan-Boltzmann constant per exposed face, W/K^4 (scaled to cell size).
	float StefanBoltzmann { get; }

	// Background temperature of space, K.
	float SpaceTemperature { get; }

	// Solar power arriving on a face pointing straight at the sun, W.
	float SolarFlux { get; }

	// Seconds per internal step.
	float FixedTimeStep { get; }

	int MaxStepsPerAdvance { get; }

}
