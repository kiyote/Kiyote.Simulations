using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class AtmosphereFrame : IAtmosphereFrame {

	private readonly IGasRegistry _gases;
	private readonly IGridChunkLayout _layout;
	private readonly IGridLayer<float> _indexer;
	private readonly float[][] _gas;
	private readonly float[]?[] _condensate;
	private float[] _pressure;
	private float[] _temperature;
	private float[] _windX;
	private float[] _windY;

	public AtmosphereFrame(
		IGasRegistry gases,
		IGridChunkLayout layout,
		IGridLayer<float> indexer,
		IGridLayer<float>?[] condensate
	) {
		_gases = gases;
		_layout = layout;
		_indexer = indexer;
		_gas = new float[condensate.Length][];
		_condensate = new float[]?[condensate.Length];
		for( int g = 0; g < condensate.Length; g++ ) {
			_gas[g] = [];
			if( condensate[g] is not null ) {
				_condensate[g] = [];
			}
		}
		_pressure = [];
		_temperature = [];
		_windX = [];
		_windY = [];
	}

	public IGasRegistry Gases => _gases;

	public long StepCount { get; private set; }

	public float Vented { get; private set; }

	public ReadOnlySpan<float> Pressure => _pressure;

	public ReadOnlySpan<float> Temperature => _temperature;

	public ReadOnlySpan<float> WindX => _windX;

	public ReadOnlySpan<float> WindY => _windY;

	public ReadOnlySpan<float> GetGas(
		GasIndex gas
	) {
		return _gas[gas.Value];
	}

	public ReadOnlySpan<float> GetCondensate(
		GasIndex gas
	) {
		return _condensate[gas.Value]
			?? throw new ArgumentException( $"Gas '{_gases.GetDefinition( gas ).Id}' does not condense.", nameof( gas ) );
	}

	public int IndexOf(
		int column,
		int row
	) {
		if( !_layout.TryGetCell( column, row, out int slot, out int local ) ) {
			return -1;
		}
		int size = _layout.ChunkSize;
		return _indexer.IndexOf( slot, local % size, local / size );
	}

	// Called only by the simulation thread while it owns this frame.
	internal void Capture(
		long step,
		float vented,
		IGridLayer<float> pressure,
		IGridLayer<float> temperature,
		IGridLayer<float> windX,
		IGridLayer<float> windY,
		IGridLayer<float>[] gas,
		IGridLayer<float>?[] condensate
	) {
		StepCount = step;
		Vented = vented;
		Copy( pressure, ref _pressure );
		Copy( temperature, ref _temperature );
		Copy( windX, ref _windX );
		Copy( windY, ref _windY );
		for( int g = 0; g < gas.Length; g++ ) {
			Copy( gas[g], ref _gas[g] );
			IGridLayer<float>? layer = condensate[g];
			if( layer is not null ) {
				float[] values = _condensate[g] ?? [];
				Copy( layer, ref values );
				_condensate[g] = values;
			}
		}
	}

	private static void Copy(
		IGridLayer<float> source,
		ref float[] destination
	) {
		ReadOnlySpan<float> cells = source.Cells;
		if( destination.Length != cells.Length ) {
			destination = new float[cells.Length];
		}
		cells.CopyTo( destination );
	}

}
