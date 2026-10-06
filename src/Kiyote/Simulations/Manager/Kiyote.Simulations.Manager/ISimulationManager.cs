namespace Kiyote.Simulations.Manager;

public interface ISimulationManager {

	bool Running { get; set; }

	void Update(
		double timeScale
	);

}
