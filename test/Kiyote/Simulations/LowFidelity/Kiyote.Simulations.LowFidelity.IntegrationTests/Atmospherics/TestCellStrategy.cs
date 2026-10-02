using Kiyote.Simulations.LowFidelity.IntegrationTests;

namespace Kiyote.Simulations.LowFidelity.Atmospherics.IntegrationTests;

internal readonly struct TestCellStrategy : IAtmosphereCellStrategy<TestCell> {
	public float GetCondensate( in TestCell cell, GasIndex gas ) {
		return 0.0f;
	}

	public float GetGas( in TestCell cell, GasIndex gas ) {
		return 0.0f;
	}

	public float GetTemperature( in TestCell cell ) {
		return 273.15f;
	}

	public bool IsPermeable( in TestCell cell ) {
		return cell.IsGasPermeable;
	}

	public void SetCondensate( ref TestCell cell, GasIndex gas, float amount ) {
		return;
	}

	public void SetGas( ref TestCell cell, GasIndex gas, float amount ) {
		return;
	}

	public void SetTemperature( ref TestCell cell, float temperature ) {
		return;
	}
}
