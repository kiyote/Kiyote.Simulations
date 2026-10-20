using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class Atmosphere<TCell, TStrategy> : IAtmosphere
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private const float MinimumTemperature = 2.7f;
	private const float FullRebuildCoverage = 0.25f;

	private static readonly int EastNeighbour = Direction.East.ToNeighbourIndex();
	private static readonly int SouthNeighbour = Direction.South.ToNeighbourIndex();

	private readonly ICompiledGridAssembly<TCell> _compiled;
	private readonly OwnedGridLayers<TCell> _layers;
	private readonly TStrategy _strategy;
	private readonly IGasRegistry _gases;
	private readonly IAtmosphericsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IConnectivityBuilder _connectivityBuilder;
	private readonly IAtmospherePressure _pressureStage;
	private readonly IAtmosphereAcceleration _acceleration;
	private readonly IAtmosphereLimiter _limiter;
	private readonly IAtmosphereTransport _transport;
	private readonly IAtmosphereThermal _thermal;
	private readonly IAtmosphereCondensation _condensation;
	private readonly IAtmosphereWind _wind;
	private readonly AtmosphereFrames _frames;
	private readonly float[] _condensationPoints;
	private readonly List<Rect> _pendingAreas;
	private readonly List<int> _work;
	// Per-chunk scratch: chunk spans for the transport hand-off, padded spans for neighbour reads.
	private readonly float[] _outgoing;
	private readonly float[] _newTotal;
	private readonly float[] _pressurePadded;
	private readonly float[] _totalPadded;
	private readonly float[] _flowEastPadded;
	private readonly float[] _flowSouthPadded;
	private readonly float[] _scalePadded;
	private readonly float[] _gasPadded;
	private readonly float[] _temperaturePadded;
	private bool _pendingFullRebuild;
	private float _accumulator;
	private float _vented;
	private long _stepCount;
	private bool _disposed;

	private IGridLayer<float>[] _gas;
	private IGridLayer<float>[] _gasNext;
	private IGridLayer<float>?[] _condensate;
	private IGridLayer<float> _temperature;
	private IGridLayer<float> _temperatureNext;
	private IGridLayer<float> _total;
	private IGridLayer<float> _pressure;
	private IGridLayer<float> _flowEast;
	private IGridLayer<float> _flowSouth;
	private IGridLayer<float> _vent;
	private IGridLayer<float> _scale;
	private IGridLayer<float> _windX;
	private IGridLayer<float> _windY;
	private IGridLayer<bool> _permeable;
	private IGridLayer<Direction> _connectivity;
	private IGridLayer<Direction> _vacuum;
	private bool[] _active;
	private bool[] _processed;
	private bool[] _publish;

	public Atmosphere(
		ICompiledGridAssembly<TCell> compiled,
		TStrategy strategy,
		IGasRegistry gases,
		IAtmosphericsSettings settings,
		ISimulationClock clock,
		IConnectivityBuilder connectivityBuilder,
		IAtmospherePressure pressureStage,
		IAtmosphereAcceleration acceleration,
		IAtmosphereLimiter limiter,
		IAtmosphereTransport transport,
		IAtmosphereThermal thermal,
		IAtmosphereCondensation condensation,
		IAtmosphereWind wind
	) {
		_compiled = compiled;
		_layers = new OwnedGridLayers<TCell>( compiled, ChunkSize );
		_strategy = strategy;
		_gases = gases;
		_settings = settings;
		_clock = clock;
		_connectivityBuilder = connectivityBuilder;
		_pressureStage = pressureStage;
		_acceleration = acceleration;
		_limiter = limiter;
		_transport = transport;
		_thermal = thermal;
		_condensation = condensation;
		_wind = wind;
		_frames = new AtmosphereFrames( gases );
		_condensationPoints = new float[gases.Count];
		for( int g = 0; g < gases.Count; g++ ) {
			_condensationPoints[g] = gases.GetDefinition( new GasIndex( g ) ).CondensationPoint ?? 0.0f;
		}
		_pendingAreas = [];
		_work = [];
		_outgoing = new float[ChunkArea];
		_newTotal = new float[ChunkArea];
		_pressurePadded = new float[PaddedArea];
		_totalPadded = new float[PaddedArea];
		_flowEastPadded = new float[PaddedArea];
		_flowSouthPadded = new float[PaddedArea];
		_scalePadded = new float[PaddedArea];
		_gasPadded = new float[PaddedArea];
		_temperaturePadded = new float[PaddedArea];
		_active = [];
		_processed = [];
		_publish = [];
		bool initialized = false;
		try {
			BindLayers();
			initialized = true;
		} finally {
			if( !initialized ) {
				_layers.Dispose();
			}
		}
	}

	IGasRegistry IAtmosphereState.Gases => _gases;

	IGridLayer<float> IAtmosphereState.Pressure => _pressure;

	IGridLayer<float> IAtmosphereState.Temperature => _temperature;

	IGridLayer<float> IAtmosphereState.WindX => _windX;

	IGridLayer<float> IAtmosphereState.WindY => _windY;

	float IAtmosphereState.Vented => _vented;

	IAtmosphereFrames IAtmosphereState.Frames => _frames;

	IGridLayer<float> IAtmosphereState.GetGas(
		GasIndex gas
	) {
		return _gas[gas.Value];
	}

	IGridLayer<float> IAtmosphereState.GetCondensate(
		GasIndex gas
	) {
		return GetCondensateLayer( gas );
	}

	Vector IAtmosphereState.GetWind(
		int column,
		int row
	) {
		if( !TryLocate( column, row, out int chunk, out int local ) ) {
			return new Vector();
		}
		return new Vector( _windX.GetValues( chunk )[local], _windY.GetValues( chunk )[local] );
	}

	int IAtmosphere.Update(
		TimeSpan elapsed
	) {
		PrepareUpdate();

		float dt = _clock.FixedTimeStep;
		_accumulator += (float)elapsed.TotalSeconds;
		int steps = (int)( _accumulator / dt );
		if( steps > _settings.MaxStepsPerUpdate ) {
			steps = _settings.MaxStepsPerUpdate;
			_accumulator = 0.0f;
		} else {
			_accumulator -= steps * dt;
		}

		for( int i = 0; i < steps; i++ ) {
			Step( dt );
		}
		Publish();

		return steps;
	}

	void IAtmosphere.InvalidateTopology(
		Rect area
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_pendingAreas.Add( area );
	}

	void IAtmosphere.InvalidateTopology() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_pendingFullRebuild = true;
	}

	void IAtmosphereState.AddGas(
		int column,
		int row,
		GasIndex gas,
		float amount,
		float? temperature
	) {
		if( amount <= 0.0f || !TryLocate( column, row, out int chunk, out int local ) ) {
			return;
		}
		float total = GetTotal( chunk, local );
		ref float t = ref _temperature.GetValues( chunk )[local];
		float incoming = temperature ?? t;
		t = total + amount > 0.0f
			? ( ( total * t ) + ( amount * incoming ) ) / ( total + amount )
			: incoming;
		_gas[gas.Value].GetValues( chunk )[local] += amount;
		Touch( chunk, _gas[gas.Value] );
		Touch( chunk, _temperature );
	}

	float IAtmosphereState.RemoveGas(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		if( amount <= 0.0f || !TryLocate( column, row, out int chunk, out int local ) ) {
			return 0.0f;
		}
		ref float value = ref _gas[gas.Value].GetValues( chunk )[local];
		float removed = MathF.Min( amount, value );
		value -= removed;
		Touch( chunk, _gas[gas.Value] );
		return removed;
	}

	float IAtmosphereState.AddEnergy(
		int column,
		int row,
		float joules
	) {
		if( !TryLocate( column, row, out int chunk, out int local ) ) {
			return 0.0f;
		}
		float total = GetTotal( chunk, local );
		if( total <= 0.0f ) {
			return 0.0f;
		}
		float capacity = total * _settings.HeatCapacity;
		ref float t = ref _temperature.GetValues( chunk )[local];
		float next = MathF.Max( MinimumTemperature, t + ( joules / capacity ) );
		float applied = ( next - t ) * capacity;
		t = next;
		Touch( chunk, _temperature );
		return applied;
	}

	float IAtmosphereState.GetTotalGas(
		int column,
		int row
	) {
		return TryLocate( column, row, out int chunk, out int local ) ? GetTotal( chunk, local ) : 0.0f;
	}

	void IAtmosphereState.AddCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		IGridLayer<float> layer = GetCondensateLayer( gas );
		if( amount <= 0.0f || !TryLocate( column, row, out int chunk, out int local ) ) {
			return;
		}
		layer.GetValues( chunk )[local] += amount;
		Touch( chunk, layer );
	}

	float IAtmosphereState.RemoveCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		IGridLayer<float> layer = GetCondensateLayer( gas );
		if( amount <= 0.0f || !TryLocate( column, row, out int chunk, out int local ) ) {
			return 0.0f;
		}
		ref float value = ref layer.GetValues( chunk )[local];
		float removed = MathF.Min( amount, value );
		value -= removed;
		Touch( chunk, layer );
		return removed;
	}

	float IAtmosphereState.GetFraction(
		int column,
		int row,
		GasIndex gas
	) {
		if( !TryLocate( column, row, out int chunk, out int local ) ) {
			return 0.0f;
		}
		float total = GetTotal( chunk, local );
		return total > 0.0f ? _gas[gas.Value].GetValues( chunk )[local] / total : 0.0f;
	}

	void IDisposable.Dispose() {
		if( _disposed ) {
			return;
		}
		_layers.Dispose();
		_disposed = true;
	}

	internal void PrepareUpdate() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_layers.EnsureUsable();
		EnsureChunkCapacity();
		ApplyTopologyChanges();
		_vented = 0.0f;
	}

	[System.Diagnostics.CodeAnalysis.MemberNotNull(
		nameof( _gas ), nameof( _gasNext ), nameof( _condensate ),
		nameof( _temperature ), nameof( _temperatureNext ), nameof( _total ), nameof( _pressure ),
		nameof( _flowEast ), nameof( _flowSouth ), nameof( _vent ), nameof( _scale ),
		nameof( _windX ), nameof( _windY ), nameof( _permeable ), nameof( _connectivity ), nameof( _vacuum )
	)]
	private void BindLayers() {
		int count = _gases.Count;
		_gas = new IGridLayer<float>[count];
		_gasNext = new IGridLayer<float>[count];
		_condensate = new IGridLayer<float>?[count];
		for( int g = 0; g < count; g++ ) {
			GasIndex gas = new GasIndex( g );
			_gas[g] = _layers.Add( _compiled.Bind<float, GasBinding<TCell, TStrategy>>( new GasBinding<TCell, TStrategy>( _strategy, gas ) ) );
			_gasNext[g] = _layers.Add( _compiled.CreateLayer<float>() );
			if( _gases.GetDefinition( gas ).CondensationPoint.HasValue ) {
				_condensate[g] = _layers.Add( _compiled.Bind<float, CondensateBinding<TCell, TStrategy>>( new CondensateBinding<TCell, TStrategy>( _strategy, gas ) ) );
			}
		}
		_temperature = _layers.Add( _compiled.Bind<float, TemperatureBinding<TCell, TStrategy>>( new TemperatureBinding<TCell, TStrategy>( _strategy ) ) );
		_temperatureNext = _layers.Add( _compiled.CreateLayer<float>() );
		_total = _layers.Add( _compiled.CreateLayer<float>() );
		_pressure = _layers.Add( _compiled.CreateLayer<float>() );
		_flowEast = _layers.Add( _compiled.CreateLayer<float>() );
		_flowSouth = _layers.Add( _compiled.CreateLayer<float>() );
		_vent = _layers.Add( _compiled.CreateLayer<float>() );
		_scale = _layers.Add( _compiled.CreateLayer<float>() );
		_windX = _layers.Add( _compiled.CreateLayer<float>() );
		_windY = _layers.Add( _compiled.CreateLayer<float>() );
		_permeable = _layers.Add( _compiled.Bind<bool, PermeabilityBinding<TCell, TStrategy>>( new PermeabilityBinding<TCell, TStrategy>( _strategy ) ) );
		_connectivity = _layers.Add( _connectivityBuilder.Build( _compiled, new PermeabilityConnectivity<TCell, TStrategy>( _strategy ) ) );
		_vacuum = _layers.Add( _compiled.CreateVacuumLayer() );

		int capacity = _compiled.ChunkLayout.ChunkCapacity;
		_active = new bool[capacity];
		_processed = new bool[capacity];
		_publish = new bool[capacity];
		Array.Fill( _active, true );
		Array.Fill( _publish, true );
		_pendingAreas.Clear();
		_pendingFullRebuild = false;
		Publish();
	}

	// Cell edits can allocate chunks while the compiled assembly is live.
	private void EnsureChunkCapacity() {
		int capacity = _compiled.ChunkLayout.ChunkCapacity;
		if( capacity <= _active.Length ) {
			return;
		}
		int previous = _active.Length;
		Array.Resize( ref _active, capacity );
		Array.Resize( ref _processed, capacity );
		Array.Resize( ref _publish, capacity );
		Array.Fill( _active, true, previous, capacity - previous );
		Array.Fill( _publish, true, previous, capacity - previous );
	}

	private void ApplyTopologyChanges() {
		if( !_pendingFullRebuild && _pendingAreas.Count == 0 ) {
			return;
		}
		GridLayout layout = _compiled.ChunkLayout;
		long covered = 0;
		foreach( Rect area in _pendingAreas ) {
			covered += (long)area.Width * area.Height;
		}
		long size = layout.TryGetPopulatedBounds( out _, out _, out int width, out int height )
			? (long)width * height
			: 0;
		PermeabilityConnectivity<TCell, TStrategy> connectivity = new PermeabilityConnectivity<TCell, TStrategy>( _strategy );

		if( _pendingFullRebuild || covered > size * FullRebuildCoverage ) {
			_layers.Remove( _connectivity );
			_connectivity = _layers.Add( _connectivityBuilder.Build( _compiled, connectivity ) );
			Array.Fill( _active, true );
			Array.Fill( _publish, true );
		} else {
			foreach( Rect area in _pendingAreas ) {
				_connectivityBuilder.Update( _compiled, _connectivity, connectivity, area );
				Wake( area );
			}
		}

		_layers.Remove( _permeable );
		_permeable = _layers.Add( _compiled.Bind<bool, PermeabilityBinding<TCell, TStrategy>>( new PermeabilityBinding<TCell, TStrategy>( _strategy ) ) );
		_layers.Remove( _vacuum );
		_vacuum = _layers.Add( _compiled.CreateVacuumLayer() );

		_pendingAreas.Clear();
		_pendingFullRebuild = false;
	}

	private void Wake(
		Rect area
	) {
		GridLayout layout = _compiled.ChunkLayout;
		int shift = layout.ChunkShift;
		for( int cy = ( area.Y1 - 1 ) >> shift; cy <= ( area.Y2 + 1 ) >> shift; cy++ ) {
			for( int cx = ( area.X1 - 1 ) >> shift; cx <= ( area.X2 + 1 ) >> shift; cx++ ) {
				if( layout.TryGetChunk( cx, cy, out int chunk ) ) {
					_active[chunk] = true;
				}
			}
		}
	}

	internal void Step(
		float dt
	) {
		_stepCount++;
		BuildWorkSet();
		if( _work.Count == 0 ) {
			return;
		}

		// Wind in neighbouring chunks reads these chunks' flows across the chunk border.
		GridLayout layout = _compiled.ChunkLayout;
		foreach( int chunk in _work ) {
			foreach( int neighbour in layout.GetNeighbours( chunk ) ) {
				if( neighbour >= 0 ) {
					_publish[neighbour] = true;
				}
			}
		}

		foreach( int chunk in _work ) {
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			Span<float> total = _total.GetValues( chunk );
			SumGas( presence, chunk, total );
			_pressureStage.Compute( presence, total, _temperature.GetValues( chunk ), _pressure.GetValues( chunk ) );
		}

		float k = _settings.Acceleration * dt;
		float damp = MathF.Max( 0.0f, 1.0f - ( _settings.Friction * dt ) );
		foreach( int chunk in _work ) {
			ReadOnlySpan<int> neighbours = layout.GetNeighbours( chunk );
			Gather( _pressure, chunk, _pressurePadded );
			_acceleration.Accelerate(
				layout.GetPresence( chunk ),
				k,
				damp,
				IsProcessed( neighbours[EastNeighbour] ),
				IsProcessed( neighbours[SouthNeighbour] ),
				_pressurePadded,
				_flowEast.GetValues( chunk ),
				_flowSouth.GetValues( chunk ),
				_vent.GetValues( chunk ),
				_connectivity.GetValues( chunk ),
				_vacuum.GetValues( chunk ),
				_permeable.GetValues( chunk )
			);
		}

		foreach( int chunk in _work ) {
			Gather( _flowEast, chunk, _flowEastPadded );
			Gather( _flowSouth, chunk, _flowSouthPadded );
			_limiter.ComputeScale(
				layout.GetPresence( chunk ),
				dt,
				_total.GetValues( chunk ),
				_flowEastPadded,
				_flowSouthPadded,
				_vent.GetValues( chunk ),
				_scale.GetValues( chunk )
			);
		}

		foreach( int chunk in _work ) {
			Gather( _scale, chunk, _scalePadded );
			_limiter.Limit(
				layout.GetPresence( chunk ),
				_flowEast.GetValues( chunk ),
				_flowSouth.GetValues( chunk ),
				_vent.GetValues( chunk ),
				_scalePadded
			);
		}

		foreach( int chunk in _work ) {
			_active[chunk] = Transfer( layout.GetPresence( chunk ), chunk, dt );
		}

		foreach( int chunk in _work ) {
			for( int g = 0; g < _gas.Length; g++ ) {
				_gasNext[g].GetValues( chunk ).CopyTo( _gas[g].GetValues( chunk ) );
				_gas[g].MarkDirty( chunk );
			}
			_temperatureNext.GetValues( chunk ).CopyTo( _temperature.GetValues( chunk ) );
			_temperature.MarkDirty( chunk );
			if( !_active[chunk] ) {
				_flowEast.GetValues( chunk ).Clear();
				_flowSouth.GetValues( chunk ).Clear();
				_vent.GetValues( chunk ).Clear();
			}
		}
	}

	private void BuildWorkSet() {
		GridLayout layout = _compiled.ChunkLayout;
		Array.Clear( _processed );
		_work.Clear();
		foreach( int chunk in layout.EnumerateChunks() ) {
			if( !_active[chunk] ) {
				continue;
			}
			if( IsEmpty( layout, chunk ) ) {
				_active[chunk] = false;
				continue;
			}
			foreach( int neighbour in layout.GetNeighbours( chunk ) ) {
				if( neighbour >= 0 && ( neighbour == chunk || !IsEmpty( layout, neighbour ) ) ) {
					AddWork( neighbour );
				}
			}
		}
	}

	private void AddWork(
		int chunk
	) {
		if( !_processed[chunk] ) {
			_processed[chunk] = true;
			_work.Add( chunk );
		}
	}

	// Returns whether the chunk is still changing.
	private bool Transfer(
		ReadOnlySpan<ulong> presence,
		int chunk,
		float dt
	) {
		float conduction = MathF.Min( _settings.Conduction * dt, 1.0f ) * 0.25f;
		float condensation = MathF.Min( _settings.CondensationRate * dt, 1.0f );

		Gather( _flowEast, chunk, _flowEastPadded );
		Gather( _flowSouth, chunk, _flowSouthPadded );
		Gather( _total, chunk, _totalPadded );
		Gather( _temperature, chunk, _temperaturePadded );
		_transport.Prepare( presence, dt, _flowEastPadded, _flowSouthPadded, _vent.GetValues( chunk ), _outgoing, _newTotal );
		for( int g = 0; g < _gas.Length; g++ ) {
			Gather( _gas[g], chunk, _gasPadded );
			_transport.Transport( presence, dt, _gasPadded, _totalPadded, _flowEastPadded, _flowSouthPadded, _outgoing, _gasNext[g].GetValues( chunk ), _newTotal );
		}

		Span<float> temperatureNext = _temperatureNext.GetValues( chunk );
		bool changing = _thermal.Mix(
			presence,
			dt,
			conduction,
			_total.GetValues( chunk ),
			_temperaturePadded,
			temperatureNext,
			_flowEastPadded,
			_flowSouthPadded,
			_vent.GetValues( chunk ),
			_connectivity.GetValues( chunk ),
			_outgoing,
			_newTotal,
			ref _vented
		);

		for( int g = 0; g < _gas.Length; g++ ) {
			IGridLayer<float>? condensate = _condensate[g];
			if( condensate is null ) {
				continue;
			}
			changing |= _condensation.Condense(
				presence,
				condensation,
				_condensationPoints[g],
				temperatureNext,
				_gasNext[g].GetValues( chunk ),
				condensate.GetValues( chunk ),
				out bool modified
			);
			if( modified ) {
				condensate.MarkDirty( chunk );
			}
		}
		return changing;
	}

	// Recomputes pressure and wind for chunks touched since the last publish, then publishes frames.
	internal void Publish() {
		GridLayout layout = _compiled.ChunkLayout;
		float windScale = _settings.WindScale * 0.5f;

		foreach( int chunk in layout.EnumerateChunks() ) {
			if( !_publish[chunk] || IsEmpty( layout, chunk ) ) {
				continue;
			}
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			// Pressure temporarily holds the total gas before being converted.
			Span<float> pressure = _pressure.GetValues( chunk );
			SumGas( presence, chunk, pressure );
			Gather( _flowEast, chunk, _flowEastPadded );
			Gather( _flowSouth, chunk, _flowSouthPadded );
			_wind.Publish(
				presence,
				windScale,
				_temperature.GetValues( chunk ),
				_flowEastPadded,
				_flowSouthPadded,
				pressure,
				_windX.GetValues( chunk ),
				_windY.GetValues( chunk )
			);
		}
		Array.Clear( _publish );

		_frames.Publish( _stepCount, _vented, _pressure, _temperature, _windX, _windY, _gas, _condensate );
	}

	private void SumGas(
		ReadOnlySpan<ulong> presence,
		int chunk,
		Span<float> total
	) {
		total.Clear();
		for( int g = 0; g < _gas.Length; g++ ) {
			_pressureStage.Accumulate( presence, _gas[g].GetValues( chunk ), total );
		}
	}

	private IGridLayer<float> GetCondensateLayer(
		GasIndex gas
	) {
		return _condensate[gas.Value]
			?? throw new ArgumentException( $"Gas '{_gases.GetDefinition( gas ).Id}' does not condense.", nameof( gas ) );
	}

	private float GetTotal(
		int chunk,
		int local
	) {
		float total = 0.0f;
		for( int g = 0; g < _gas.Length; g++ ) {
			total += _gas[g].GetValues( chunk )[local];
		}
		return total;
	}

	private bool TryLocate(
		int column,
		int row,
		out int chunk,
		out int local
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		return _compiled.ChunkLayout.TryLocate( column, row, out chunk, out local );
	}

	private bool IsProcessed(
		int chunk
	) {
		return chunk >= 0 && _processed[chunk];
	}

	private void Touch(
		int chunk,
		IGridLayer layer
	) {
		layer.MarkDirty( chunk );
		EnsureChunkCapacity();
		_active[chunk] = true;
		_publish[chunk] = true;
	}

}
