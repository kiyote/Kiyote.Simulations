using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal readonly struct GasBinding<TCell, TStrategy> : IGridLayerBinding<TCell, float>
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private readonly TStrategy _strategy;
	private readonly GasIndex _gas;

	public GasBinding(
		TStrategy strategy,
		GasIndex gas
	) {
		_strategy = strategy;
		_gas = gas;
	}

	float IGridLayerBinding<TCell, float>.Extract(
		in TCell cell
	) {
		return _strategy.GetGas( cell, _gas );
	}

	void IGridLayerBinding<TCell, float>.Commit(
		ref TCell cell,
		float value
	) {
		_strategy.SetGas( ref cell, _gas, value );
	}

}

internal readonly struct CondensateBinding<TCell, TStrategy> : IGridLayerBinding<TCell, float>
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private readonly TStrategy _strategy;
	private readonly GasIndex _gas;

	public CondensateBinding(
		TStrategy strategy,
		GasIndex gas
	) {
		_strategy = strategy;
		_gas = gas;
	}

	float IGridLayerBinding<TCell, float>.Extract(
		in TCell cell
	) {
		return _strategy.GetCondensate( cell, _gas );
	}

	void IGridLayerBinding<TCell, float>.Commit(
		ref TCell cell,
		float value
	) {
		_strategy.SetCondensate( ref cell, _gas, value );
	}

}

internal readonly struct TemperatureBinding<TCell, TStrategy> : IGridLayerBinding<TCell, float>
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private readonly TStrategy _strategy;

	public TemperatureBinding(
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

// Read-only: permeability is owned by the cells and never written back.
internal readonly struct PermeabilityBinding<TCell, TStrategy> : IGridLayerBinding<TCell, bool>
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private readonly TStrategy _strategy;

	public PermeabilityBinding(
		TStrategy strategy
	) {
		_strategy = strategy;
	}

	bool IGridLayerBinding<TCell, bool>.Extract(
		in TCell cell
	) {
		return _strategy.IsPermeable( cell );
	}

	void IGridLayerBinding<TCell, bool>.Commit(
		ref TCell cell,
		bool value
	) {
	}

}

internal readonly struct PermeabilityConnectivity<TCell, TStrategy> : IConnectivityStrategy<TCell>
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private readonly TStrategy _strategy;

	public PermeabilityConnectivity(
		TStrategy strategy
	) {
		_strategy = strategy;
	}

	bool IConnectivityStrategy<TCell>.Evaluate(
		in TopologyCell<TCell> source,
		in TopologyCell<TCell> destination,
		Direction direction,
		in TopologyCell<TCell> orthogonalA,
		in TopologyCell<TCell> orthogonalB,
		bool isSeam
	) {
		return source.IsOccupied
			&& destination.IsOccupied
			&& _strategy.IsPermeable( source.Cell! )
			&& _strategy.IsPermeable( destination.Cell! );
	}

}
