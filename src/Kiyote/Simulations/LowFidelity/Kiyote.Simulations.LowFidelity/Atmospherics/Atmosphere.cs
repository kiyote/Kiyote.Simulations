using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class Atmosphere<TCell, TStrategy> : IAtmosphere
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private const int Halo = 1;
	private const float MinimumTemperature = 2.7f;
	private const float FullRebuildCoverage = 0.25f;
	private const int FrameIndexMask = 0b11;
	private const int FrameFresh = 0b100;

	private static readonly Direction[] Neighbours = [
		Direction.North,
		Direction.NorthEast,
		Direction.East,
		Direction.SouthEast,
		Direction.South,
		Direction.SouthWest,
		Direction.West,
		Direction.NorthWest
	];

	private readonly IGridAssembly<TCell> _assembly;
	private readonly TStrategy _strategy;
	private readonly IGasRegistry _gases;
	private readonly IAtmosphericsSettings _settings;
	private readonly IGridCompiler _compiler;
	private readonly IConnectivityBuilder _connectivityBuilder;
	private readonly IAtmospherePressure _pressureStage;
	private readonly IAtmosphereAcceleration _acceleration;
	private readonly IAtmosphereLimiter _limiter;
	private readonly IAtmosphereTransport _transport;
	private readonly IAtmosphereThermal _thermal;
	private readonly IAtmosphereCondensation _condensation;
	private readonly IAtmosphereWind _wind;
	private readonly List<Rect> _pendingAreas;
	private readonly List<int> _work;
	private readonly float[] _outgoing;
	private readonly float[] _newTotal;
	private bool _pendingFullRebuild;
	private float _accumulator;
	private float _vented;
	private bool _disposed;

	private ICompiledGridAssembly<TCell> _compiled;
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

	// Triple buffer: the simulation owns _frames[_frameBack], the reader owns _frames[_frameFront],
	// and _frameShared holds the index of the third plus FrameFresh when it is newer than the reader's.
	private AtmosphereFrame[] _frames;
	private int _frameBack;
	private int _frameShared;
	private int _frameFront;
	private bool _frameHeld;
	private long _stepCount;

	public Atmosphere(
		IGridAssembly<TCell> assembly,
		TStrategy strategy,
		IGasRegistry gases,
		IAtmosphericsSettings settings,
		IGridCompiler compiler,
		IConnectivityBuilder connectivityBuilder,
		IAtmospherePressure pressureStage,
		IAtmosphereAcceleration acceleration,
		IAtmosphereLimiter limiter,
		IAtmosphereTransport transport,
		IAtmosphereThermal thermal,
		IAtmosphereCondensation condensation,
		IAtmosphereWind wind
	) {
		_assembly = assembly;
		_strategy = strategy;
		_gases = gases;
		_settings = settings;
		_compiler = compiler;
		_connectivityBuilder = connectivityBuilder;
		_pressureStage = pressureStage;
		_acceleration = acceleration;
		_limiter = limiter;
		_transport = transport;
		_thermal = thermal;
		_condensation = condensation;
		_wind = wind;
		_pendingAreas = [];
		_work = [];
		_outgoing = new float[ChunkSize * ChunkSize];
		_newTotal = new float[ChunkSize * ChunkSize];
		_active = [];
		_processed = [];
		_publish = [];
		Compile();
	}

	IGasRegistry IAtmosphere.Gases => _gases;

	IGridLayer<float> IAtmosphere.Pressure => _pressure;

	IGridLayer<float> IAtmosphere.Temperature => _temperature;

	IGridLayer<float> IAtmosphere.WindX => _windX;

	IGridLayer<float> IAtmosphere.WindY => _windY;

	float IAtmosphere.Vented => _vented;

	IGridLayer<float> IAtmosphere.GetGas(
		GasIndex gas
	) {
		return _gas[gas.Value];
	}

	IGridLayer<float> IAtmosphere.GetCondensate(
		GasIndex gas
	) {
		return GetCondensateLayer( gas );
	}

	Vector IAtmosphere.GetWind(
		int column,
		int row
	) {
		if( !TryGetIndex( column, row, out _, out int index ) ) {
			return new Vector();
		}
		return new Vector( _windX.Cells[index], _windY.Cells[index] );
	}

	int IAtmosphere.Advance(
		TimeSpan elapsed
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );

		if( _compiled.IsStale ) {
			_compiled.Commit();
			_compiled.Dispose();
			Compile();
		}
		EnsureSlotCapacity();
		ApplyTopologyChanges();

		float dt = _settings.FixedTimeStep;
		_accumulator += (float)elapsed.TotalSeconds;
		int steps = (int)( _accumulator / dt );
		if( steps > _settings.MaxStepsPerAdvance ) {
			steps = _settings.MaxStepsPerAdvance;
			_accumulator = 0.0f;
		} else {
			_accumulator -= steps * dt;
		}

		_vented = 0.0f;
		for( int i = 0; i < steps; i++ ) {
			Step( dt );
		}
		Publish();

		return steps;
	}

	IAtmosphereFrame IAtmosphere.AcquireFrame() {
		if( _frameHeld ) {
			throw new InvalidOperationException( "A frame is already held; release it before acquiring another." );
		}
		if( ( Volatile.Read( ref _frameShared ) & FrameFresh ) != 0 ) {
			_frameFront = Interlocked.Exchange( ref _frameShared, _frameFront ) & FrameIndexMask;
		}
		_frameHeld = true;
		return _frames[_frameFront];
	}

	void IAtmosphere.ReleaseFrame(
		IAtmosphereFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if( !_frameHeld ) {
			throw new InvalidOperationException( "No frame is held." );
		}
		_frameHeld = false;
	}

	void IAtmosphere.Commit() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_compiled.Commit();
	}

	void IAtmosphere.InvalidateTopology(
		Rect area
	) {
		_pendingAreas.Add( area );
	}

	void IAtmosphere.InvalidateTopology() {
		_pendingFullRebuild = true;
	}

	void IAtmosphere.AddGas(
		int column,
		int row,
		GasIndex gas,
		float amount,
		float? temperature
	) {
		if( amount <= 0.0f || !TryGetIndex( column, row, out int slot, out int index ) ) {
			return;
		}
		float total = GetTotal( index );
		ref float t = ref _temperature.Cells[index];
		float incoming = temperature ?? t;
		t = total + amount > 0.0f
			? ( ( total * t ) + ( amount * incoming ) ) / ( total + amount )
			: incoming;
		_gas[gas.Value].Cells[index] += amount;
		Touch( slot, _gas[gas.Value] );
		Touch( slot, _temperature );
	}

	float IAtmosphere.RemoveGas(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		if( amount <= 0.0f || !TryGetIndex( column, row, out int slot, out int index ) ) {
			return 0.0f;
		}
		ref float value = ref _gas[gas.Value].Cells[index];
		float removed = MathF.Min( amount, value );
		value -= removed;
		Touch( slot, _gas[gas.Value] );
		return removed;
	}

	float IAtmosphere.AddEnergy(
		int column,
		int row,
		float joules
	) {
		if( !TryGetIndex( column, row, out int slot, out int index ) ) {
			return 0.0f;
		}
		float total = GetTotal( index );
		if( total <= 0.0f ) {
			return 0.0f;
		}
		float capacity = total * _settings.HeatCapacity;
		ref float t = ref _temperature.Cells[index];
		float next = MathF.Max( MinimumTemperature, t + ( joules / capacity ) );
		float applied = ( next - t ) * capacity;
		t = next;
		Touch( slot, _temperature );
		return applied;
	}

	float IAtmosphere.GetTotalGas(
		int column,
		int row
	) {
		return TryGetIndex( column, row, out _, out int index ) ? GetTotal( index ) : 0.0f;
	}

	void IAtmosphere.AddCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		IGridLayer<float> layer = GetCondensateLayer( gas );
		if( amount <= 0.0f || !TryGetIndex( column, row, out int slot, out int index ) ) {
			return;
		}
		layer.Cells[index] += amount;
		Touch( slot, layer );
	}

	float IAtmosphere.RemoveCondensate(
		int column,
		int row,
		GasIndex gas,
		float amount
	) {
		IGridLayer<float> layer = GetCondensateLayer( gas );
		if( amount <= 0.0f || !TryGetIndex( column, row, out int slot, out int index ) ) {
			return 0.0f;
		}
		ref float value = ref layer.Cells[index];
		float removed = MathF.Min( amount, value );
		value -= removed;
		Touch( slot, layer );
		return removed;
	}

	float IAtmosphere.GetFraction(
		int column,
		int row,
		GasIndex gas
	) {
		if( !TryGetIndex( column, row, out _, out int index ) ) {
			return 0.0f;
		}
		float total = GetTotal( index );
		return total > 0.0f ? _gas[gas.Value].Cells[index] / total : 0.0f;
	}

	void IDisposable.Dispose() {
		if( _disposed ) {
			return;
		}
		_compiled.Dispose();
		_disposed = true;
	}

	[System.Diagnostics.CodeAnalysis.MemberNotNull(
		nameof( _compiled ), nameof( _gas ), nameof( _gasNext ), nameof( _condensate ),
		nameof( _temperature ), nameof( _temperatureNext ), nameof( _total ), nameof( _pressure ),
		nameof( _flowEast ), nameof( _flowSouth ), nameof( _vent ), nameof( _scale ),
		nameof( _windX ), nameof( _windY ), nameof( _permeable ), nameof( _connectivity ), nameof( _vacuum ), nameof( _frames )
	)]
	private void Compile() {
		_compiled = _compiler.Compile( _assembly, ChunkSize );

		int count = _gases.Count;
		_gas = new IGridLayer<float>[count];
		_gasNext = new IGridLayer<float>[count];
		_condensate = new IGridLayer<float>?[count];
		for( int g = 0; g < count; g++ ) {
			GasIndex gas = new GasIndex( g );
			_gas[g] = _compiled.Bind<float, GasBinding<TCell, TStrategy>>( new GasBinding<TCell, TStrategy>( _strategy, gas ), Halo );
			_gasNext[g] = _compiled.CreateLayer<float>( Halo );
			if( _gases.GetDefinition( gas ).CondensationPoint.HasValue ) {
				_condensate[g] = _compiled.Bind<float, CondensateBinding<TCell, TStrategy>>( new CondensateBinding<TCell, TStrategy>( _strategy, gas ), Halo );
			}
		}
		_temperature = _compiled.Bind<float, TemperatureBinding<TCell, TStrategy>>( new TemperatureBinding<TCell, TStrategy>( _strategy ), Halo );
		_temperatureNext = _compiled.CreateLayer<float>( Halo );
		_total = _compiled.CreateLayer<float>( Halo );
		_pressure = _compiled.CreateLayer<float>( Halo );
		_flowEast = _compiled.CreateLayer<float>( Halo );
		_flowSouth = _compiled.CreateLayer<float>( Halo );
		_vent = _compiled.CreateLayer<float>( Halo );
		_scale = _compiled.CreateLayer<float>( Halo );
		_windX = _compiled.CreateLayer<float>( Halo );
		_windY = _compiled.CreateLayer<float>( Halo );
		_permeable = _compiled.Bind<bool, PermeabilityBinding<TCell, TStrategy>>( new PermeabilityBinding<TCell, TStrategy>( _strategy ), Halo );
		_connectivity = _connectivityBuilder.Build( _compiled, new PermeabilityConnectivity<TCell, TStrategy>( _strategy ), Halo );
		_vacuum = _compiled.CreateVacuumLayer( Halo );

		_active = new bool[_compiled.ChunkLayout.SlotCount];
		_processed = new bool[_active.Length];
		_publish = new bool[_active.Length];
		Array.Fill( _active, true );
		Array.Fill( _publish, true );
		_pendingAreas.Clear();
		_pendingFullRebuild = false;
		_frames = [CreateFrame(), CreateFrame(), CreateFrame()];
		_frameBack = 0;
		_frameShared = 1;
		_frameFront = 2;
		_frameHeld = false;
		Publish();
	}

	private AtmosphereFrame CreateFrame() {
		return new AtmosphereFrame( _gases, _compiled.ChunkLayout, _temperature, _condensate );
	}

	private void EnsureSlotCapacity() {
		int slots = _compiled.ChunkLayout.SlotCount;
		if( slots <= _active.Length ) {
			return;
		}
		int previous = _active.Length;
		Array.Resize( ref _active, slots );
		Array.Resize( ref _processed, slots );
		Array.Resize( ref _publish, slots );
		Array.Fill( _active, true, previous, slots - previous );
		Array.Fill( _publish, true, previous, slots - previous );
	}

	private void ApplyTopologyChanges() {
		if( !_pendingFullRebuild && _pendingAreas.Count == 0 ) {
			return;
		}
		IGridChunkLayout layout = _compiled.ChunkLayout;
		_compiled.Commit();

		long covered = 0;
		foreach( Rect area in _pendingAreas ) {
			covered += (long)area.Width * area.Height;
		}
		Rect? bounds = layout.Bounds;
		long size = bounds.HasValue ? (long)bounds.Value.Width * bounds.Value.Height : 0;
		PermeabilityConnectivity<TCell, TStrategy> connectivity = new PermeabilityConnectivity<TCell, TStrategy>( _strategy );

		if( _pendingFullRebuild || covered > size * FullRebuildCoverage ) {
			_compiled.RemoveLayer( _connectivity );
			_connectivity = _connectivityBuilder.Build( _compiled, connectivity, Halo );
			Array.Fill( _active, true );
			Array.Fill( _publish, true );
		} else {
			foreach( Rect area in _pendingAreas ) {
				_connectivityBuilder.Update( _compiled, _connectivity, connectivity, area );
				Wake( area );
			}
		}

		_compiled.RemoveLayer( _permeable );
		_permeable = _compiled.Bind<bool, PermeabilityBinding<TCell, TStrategy>>( new PermeabilityBinding<TCell, TStrategy>( _strategy ), Halo );
		_compiled.RemoveLayer( _vacuum );
		_vacuum = _compiled.CreateVacuumLayer( Halo );

		_pendingAreas.Clear();
		_pendingFullRebuild = false;
	}

	private void Wake(
		Rect area
	) {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		int shift = layout.ChunkShift;
		for( int cy = ( area.Y1 - 1 ) >> shift; cy <= ( area.Y2 + 1 ) >> shift; cy++ ) {
			for( int cx = ( area.X1 - 1 ) >> shift; cx <= ( area.X2 + 1 ) >> shift; cx++ ) {
				if( layout.TryGetSlot( cx, cy, out int slot ) ) {
					_active[slot] = true;
				}
			}
		}
	}

	private void Step(
		float dt
	) {
		_stepCount++;
		BuildWorkSet();
		if( _work.Count == 0 ) {
			return;
		}

		// Wind in neighbouring chunks reads these chunks' flows through the halo.
		IGridChunkLayout layout = _compiled.ChunkLayout;
		foreach( int slot in _work ) {
			_publish[slot] = true;
			foreach( Direction direction in Neighbours ) {
				int neighbour = layout.GetNeighbour( slot, direction );
				if( neighbour >= 0 ) {
					_publish[neighbour] = true;
				}
			}
		}

		foreach( int slot in _work ) {
			for( int g = 0; g < _gas.Length; g++ ) {
				_gas[g].ExchangeHalo( slot );
			}
			_temperature.ExchangeHalo( slot );
		}

		foreach( int slot in _work ) {
			_pressureStage.Compute( slot, layout.GetValidityMask( slot ), _gas, _temperature, _total, _pressure );
		}
		ExchangeHalos( _total );
		ExchangeHalos( _pressure );

		foreach( int slot in _work ) {
			AccelerateFlows( slot, dt );
		}
		ExchangeHalos( _flowEast );
		ExchangeHalos( _flowSouth );

		foreach( int slot in _work ) {
			_limiter.ComputeScale( slot, layout.GetValidityMask( slot ), dt, _total, _flowEast, _flowSouth, _vent, _scale );
		}
		ExchangeHalos( _scale );

		foreach( int slot in _work ) {
			_limiter.Limit( slot, layout.GetValidityMask( slot ), _flowEast, _flowSouth, _vent, _scale );
		}
		ExchangeHalos( _flowEast );
		ExchangeHalos( _flowSouth );

		foreach( int slot in _work ) {
			_active[slot] = Transfer( slot, dt );
		}

		foreach( int slot in _work ) {
			for( int g = 0; g < _gas.Length; g++ ) {
				_gasNext[g].GetChunk( slot ).CopyTo( _gas[g].GetChunk( slot ) );
				_gas[g].MarkDirty( slot );
			}
			_temperatureNext.GetChunk( slot ).CopyTo( _temperature.GetChunk( slot ) );
			_temperature.MarkDirty( slot );
			if( !_active[slot] ) {
				_flowEast.GetChunk( slot ).Clear();
				_flowSouth.GetChunk( slot ).Clear();
				_vent.GetChunk( slot ).Clear();
			}
		}
	}

	private void BuildWorkSet() {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		Array.Clear( _processed );
		_work.Clear();
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( !_active[slot] ) {
				continue;
			}
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				_active[slot] = false;
				continue;
			}
			AddWork( slot );
			foreach( Direction direction in Neighbours ) {
				int neighbour = layout.GetNeighbour( slot, direction );
				if( neighbour >= 0 && layout.GetState( neighbour ) != ChunkState.Empty ) {
					AddWork( neighbour );
				}
			}
		}
	}

	private void AddWork(
		int slot
	) {
		if( !_processed[slot] ) {
			_processed[slot] = true;
			_work.Add( slot );
		}
	}

	private void ExchangeHalos<T>(
		IGridLayer<T> layer
	) {
		foreach( int slot in _work ) {
			layer.ExchangeHalo( slot );
		}
	}

	private void AccelerateFlows(
		int slot,
		float dt
	) {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		_acceleration.Accelerate(
			slot,
			layout.GetValidityMask( slot ),
			_settings.Acceleration * dt,
			MathF.Max( 0.0f, 1.0f - ( _settings.Friction * dt ) ),
			IsProcessed( layout.GetNeighbour( slot, Direction.East ) ),
			IsProcessed( layout.GetNeighbour( slot, Direction.South ) ),
			_pressure,
			_flowEast,
			_flowSouth,
			_vent,
			_connectivity,
			_vacuum,
			_permeable
		);
	}

	// Returns whether the chunk is still changing.
	private bool Transfer(
		int slot,
		float dt
	) {
		ReadOnlySpan<ulong> mask = _compiled.ChunkLayout.GetValidityMask( slot );
		float conduction = MathF.Min( _settings.Conduction * dt, 1.0f ) * 0.25f;
		float condensation = MathF.Min( _settings.CondensationRate * dt, 1.0f );
		_transport.Transport( slot, mask, dt, _gas, _gasNext, _total, _flowEast, _flowSouth, _vent, _outgoing, _newTotal );
		bool changing = _thermal.Mix( slot, mask, dt, conduction, _total, _temperature, _temperatureNext, _flowEast, _flowSouth, _vent, _connectivity, _outgoing, _newTotal, ref _vented );
		changing |= _condensation.Condense( slot, mask, condensation, _gases, _temperatureNext, _gasNext, _condensate );
		return changing;
	}

	private void Publish() {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		float windScale = _settings.WindScale * 0.5f;

		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( !_publish[slot] || layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			_flowEast.ExchangeHalo( slot );
			_flowSouth.ExchangeHalo( slot );
		}
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( !_publish[slot] || layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			ReadOnlySpan<ulong> mask = layout.GetValidityMask( slot );
			// Pressure temporarily holds the total gas before being converted.
			_pressureStage.SumGas( slot, mask, _gas, _pressure );
			_wind.Publish( slot, mask, windScale, _temperature, _flowEast, _flowSouth, _pressure, _windX, _windY );
		}
		Array.Clear( _publish );

		_frames[_frameBack].Capture( _stepCount, _vented, _pressure, _temperature, _windX, _windY, _gas, _condensate );
		_frameBack = Interlocked.Exchange( ref _frameShared, _frameBack | FrameFresh ) & FrameIndexMask;
	}

	private IGridLayer<float> GetCondensateLayer(
		GasIndex gas
	) {
		return _condensate[gas.Value]
			?? throw new ArgumentException( $"Gas '{_gases.GetDefinition( gas ).Id}' does not condense.", nameof( gas ) );
	}

	private float GetTotal(
		int index
	) {
		float total = 0.0f;
		for( int g = 0; g < _gas.Length; g++ ) {
			total += _gas[g].Cells[index];
		}
		return total;
	}

	private bool TryGetIndex(
		int column,
		int row,
		out int slot,
		out int index
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( !_compiled.ChunkLayout.TryGetCell( column, row, out slot, out int local ) ) {
			index = -1;
			return false;
		}
		index = ToIndex( slot, local );
		return true;
	}

	private int ToIndex(
		int slot,
		int local
	) {
		return _temperature.IndexOf( slot, local % ChunkSize, local / ChunkSize );
	}

	private bool IsProcessed(
		int slot
	) {
		return slot >= 0 && _processed[slot];
	}

	private void Touch(
		int slot,
		IGridLayer layer
	) {
		layer.MarkDirty( slot );
		EnsureSlotCapacity();
		_active[slot] = true;
		_publish[slot] = true;
	}

}
