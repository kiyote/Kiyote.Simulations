namespace Kiyote.Simulations.LowFidelity.Thermals;

public interface IThermalCellStrategy<TCell> {

	MaterialIndex GetMaterial(
		in TCell cell
	);

	float GetTemperature(
		in TCell cell
	);

	void SetTemperature(
		ref TCell cell,
		float temperature
	);

}
