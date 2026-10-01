namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public interface IAtmosphereCellStrategy<TCell> {

	bool IsPermeable(
		in TCell cell
	);

	float GetGas(
		in TCell cell,
		GasIndex gas
	);

	void SetGas(
		ref TCell cell,
		GasIndex gas,
		float amount
	);

	float GetCondensate(
		in TCell cell,
		GasIndex gas
	);

	void SetCondensate(
		ref TCell cell,
		GasIndex gas,
		float amount
	);

	float GetTemperature(
		in TCell cell
	);

	void SetTemperature(
		ref TCell cell,
		float temperature
	);

}
