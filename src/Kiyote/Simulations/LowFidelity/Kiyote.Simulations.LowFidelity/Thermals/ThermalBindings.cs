using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal readonly struct ThermalTemperatureBinding<TCell, TStrategy> : IGridLayerBinding<TCell, float>
	where TStrategy : struct, IThermalCellStrategy<TCell> {

	private readonly TStrategy _strategy;

	public ThermalTemperatureBinding(
		TStrategy strategy
	) {
		_strategy = strategy;
	}

	float IGridLayerBinding<TCell, float>.Extract(
		in TCell cell
	) {
		return _strategy.GetTemperature( cell );
	}

	void IGridLayerBinding<TCell, float>.Commit(
		ref TCell cell,
		float value
	) {
		_strategy.SetTemperature( ref cell, value );
	}

}

// Read-only: looks up one material property per cell and never writes back.
internal readonly struct MaterialPropertyBinding<TCell, TStrategy> : IGridLayerBinding<TCell, float>
	where TStrategy : struct, IThermalCellStrategy<TCell> {

	private readonly TStrategy _strategy;
	private readonly float[] _values;

	public MaterialPropertyBinding(
		TStrategy strategy,
		float[] values
	) {
		_strategy = strategy;
		_values = values;
	}

	float IGridLayerBinding<TCell, float>.Extract(
		in TCell cell
	) {
		return _values[_strategy.GetMaterial( cell ).Value];
	}

	void IGridLayerBinding<TCell, float>.Commit(
		ref TCell cell,
		float value
	) {
	}

}
