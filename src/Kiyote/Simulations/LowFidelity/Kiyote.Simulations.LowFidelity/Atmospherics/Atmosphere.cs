using System.Numerics;
using Kiyote.Geometry;
using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class Atmosphere<TCell, TStrategy> : IAtmosphere
	where TStrategy : struct, IAtmosphereCellStrategy<TCell> {

	private const int ChunkSize = 16;
	private const int Halo = 1;
	private const float ReferenceTemperature = 293.15f;
	private const float MinimumTemperature = 2.7f;
	private const float Epsilon = 1e-4f;
	private const float FullRebuildCoverage = 0.25f;
	private const Direction Cardinal = Direction.North | Direction.East | Direction.South | Direction.West;

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
	private readonly List<Rect> _pendingAreas;
	private readonly List<int> _work;
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

	public Atmosphere(
		IGridAssembly<TCell> assembly,
		TStrategy strategy,
		IGasRegistry gases,
		IAtmosphericsSettings settings,
		IGridCompiler compiler,
		IConnectivityBuilder connectivityBuilder
	) {
		_assembly = assembly;
		_strategy = strategy;
		_gases = gases;
		_settings = settings;
		_compiler = compiler;
		_connectivityBuilder = connectivityBuilder;
		_pendingAreas = [];
		_work = [];
		_active = [];
		_processed = [];
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
		nameof( _windX ), nameof( _windY ), nameof( _permeable ), nameof( _connectivity ), nameof( _vacuum )
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
		Array.Fill( _active, true );
		_pendingAreas.Clear();
		_pendingFullRebuild = false;
		Publish();
	}

	private void EnsureSlotCapacity() {
		int slots = _compiled.ChunkLayout.SlotCount;
		if( slots <= _active.Length ) {
			return;
		}
		int previous = _active.Length;
		Array.Resize( ref _active, slots );
		Array.Resize( ref _processed, slots );
		Array.Fill( _active, true, previous, slots - previous );
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
		BuildWorkSet();
		if( _work.Count == 0 ) {
			return;
		}

		foreach( int slot in _work ) {
			for( int g = 0; g < _gas.Length; g++ ) {
				_gas[g].ExchangeHalo( slot );
			}
			_temperature.ExchangeHalo( slot );
		}

		foreach( int slot in _work ) {
			ComputePressure( slot );
		}
		ExchangeHalos( _total );
		ExchangeHalos( _pressure );

		foreach( int slot in _work ) {
			AccelerateFlows( slot, dt );
		}
		ExchangeHalos( _flowEast );
		ExchangeHalos( _flowSouth );

		foreach( int slot in _work ) {
			ComputeScale( slot, dt );
		}
		ExchangeHalos( _scale );

		foreach( int slot in _work ) {
			LimitFlows( slot );
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

	private void ComputePressure(
		int slot
	) {
		Span<float> total = _total.Cells;
		Span<float> pressure = _pressure.Cells;
		Span<float> temperature = _temperature.Cells;
		ReadOnlySpan<ulong> mask = _compiled.ChunkLayout.GetValidityMask( slot );
		for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
			if( !IsValid( mask, local ) ) {
				continue;
			}
			int i = ToIndex( slot, local );
			float sum = GetTotal( i );
			total[i] = sum;
			pressure[i] = sum * temperature[i] / ReferenceTemperature;
		}
	}

	private void AccelerateFlows(
		int slot,
		float dt
	) {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		bool eastOpen = IsProcessed( layout.GetNeighbour( slot, Direction.East ) );
		bool southOpen = IsProcessed( layout.GetNeighbour( slot, Direction.South ) );
		int stride = _pressure.Stride;
		float k = _settings.Acceleration * dt;
		float damp = MathF.Max( 0.0f, 1.0f - ( _settings.Friction * dt ) );

		Span<float> pressure = _pressure.Cells;
		Span<float> flowEast = _flowEast.Cells;
		Span<float> flowSouth = _flowSouth.Cells;
		Span<float> vent = _vent.Cells;
		Span<Direction> connectivity = _connectivity.Cells;
		Span<Direction> vacuum = _vacuum.Cells;
		Span<bool> permeable = _permeable.Cells;
		ReadOnlySpan<ulong> mask = layout.GetValidityMask( slot );

		for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
			if( !IsValid( mask, local ) ) {
				continue;
			}
			int i = ToIndex( slot, local );
			int lx = local % ChunkSize;
			int ly = local / ChunkSize;
			Direction open = connectivity[i];

			flowEast[i] = ( open & Direction.East ) != 0 && ( lx < ChunkSize - 1 || eastOpen )
				? ( flowEast[i] + ( k * ( pressure[i] - pressure[i + 1] ) ) ) * damp
				: 0.0f;
			flowSouth[i] = ( open & Direction.South ) != 0 && ( ly < ChunkSize - 1 || southOpen )
				? ( flowSouth[i] + ( k * ( pressure[i] - pressure[i + stride] ) ) ) * damp
				: 0.0f;

			int faces = permeable[i] ? BitOperations.PopCount( (uint)( vacuum[i] & Cardinal ) ) : 0;
			vent[i] = faces > 0
				? MathF.Max( 0.0f, ( vent[i] + ( k * pressure[i] * faces ) ) * damp )
				: 0.0f;
		}
	}

	private void ComputeScale(
		int slot,
		float dt
	) {
		int stride = _flowEast.Stride;
		Span<float> total = _total.Cells;
		Span<float> flowEast = _flowEast.Cells;
		Span<float> flowSouth = _flowSouth.Cells;
		Span<float> vent = _vent.Cells;
		Span<float> scale = _scale.Cells;
		ReadOnlySpan<ulong> mask = _compiled.ChunkLayout.GetValidityMask( slot );

		for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
			if( !IsValid( mask, local ) ) {
				continue;
			}
			int i = ToIndex( slot, local );
			float outflow = MathF.Max( flowEast[i], 0.0f )
				+ MathF.Max( flowSouth[i], 0.0f )
				+ MathF.Max( -flowEast[i - 1], 0.0f )
				+ MathF.Max( -flowSouth[i - stride], 0.0f )
				+ vent[i];
			outflow *= dt;
			scale[i] = outflow > total[i] ? total[i] / outflow : 1.0f;
		}
	}

	private void LimitFlows(
		int slot
	) {
		int stride = _flowEast.Stride;
		Span<float> flowEast = _flowEast.Cells;
		Span<float> flowSouth = _flowSouth.Cells;
		Span<float> vent = _vent.Cells;
		Span<float> scale = _scale.Cells;
		ReadOnlySpan<ulong> mask = _compiled.ChunkLayout.GetValidityMask( slot );

		for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
			if( !IsValid( mask, local ) ) {
				continue;
			}
			int i = ToIndex( slot, local );
			flowEast[i] *= flowEast[i] > 0.0f ? scale[i] : scale[i + 1];
			flowSouth[i] *= flowSouth[i] > 0.0f ? scale[i] : scale[i + stride];
			vent[i] *= scale[i];
		}
	}

	// Returns whether the chunk is still changing.
	private bool Transfer(
		int slot,
		float dt
	) {
		int stride = _flowEast.Stride;
		float conduction = MathF.Min( _settings.Conduction * dt, 1.0f ) * 0.25f;
		float condensation = MathF.Min( _settings.CondensationRate * dt, 1.0f );
		Span<float> total = _total.Cells;
		Span<float> temperature = _temperature.Cells;
		Span<float> temperatureNext = _temperatureNext.Cells;
		Span<float> flowEast = _flowEast.Cells;
		Span<float> flowSouth = _flowSouth.Cells;
		Span<float> vent = _vent.Cells;
		Span<Direction> connectivity = _connectivity.Cells;
		ReadOnlySpan<ulong> mask = _compiled.ChunkLayout.GetValidityMask( slot );
		Span<int> donors = stackalloc int[4];
		Span<float> moves = stackalloc float[4];
		bool changing = false;

		for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
			if( !IsValid( mask, local ) ) {
				continue;
			}
			int i = ToIndex( slot, local );

			// Signed amount leaving the cell through each face; negative arrives from the neighbour.
			moves[0] = flowEast[i] * dt;
			donors[0] = i + 1;
			moves[1] = flowSouth[i] * dt;
			donors[1] = i + stride;
			moves[2] = -flowEast[i - 1] * dt;
			donors[2] = i - 1;
			moves[3] = -flowSouth[i - stride] * dt;
			donors[3] = i - stride;
			float vented = vent[i] * dt;

			float own = total[i];
			float outgoing = vented;
			float energy = 0.0f;
			float incoming = 0.0f;
			for( int f = 0; f < 4; f++ ) {
				float move = moves[f];
				if( move > 0.0f ) {
					outgoing += move;
				} else if( move < 0.0f ) {
					incoming -= move;
					energy -= move * temperature[donors[f]];
				}
			}

			float newTotal = 0.0f;
			for( int g = 0; g < _gas.Length; g++ ) {
				Span<float> amounts = _gas[g].Cells;
				float amount = amounts[i];
				float next = amount;
				if( own > 0.0f ) {
					next -= outgoing * amount / own;
				}
				for( int f = 0; f < 4; f++ ) {
					float move = moves[f];
					int donor = donors[f];
					if( move < 0.0f && total[donor] > 0.0f ) {
						next -= move * amounts[donor] / total[donor];
					}
				}
				next = MathF.Max( next, 0.0f );
				_gasNext[g].Cells[i] = next;
				newTotal += next;
			}

			float t = temperature[i];
			float remaining = MathF.Max( own - outgoing, 0.0f );
			float nextTemperature = newTotal > 0.0f
				? ( ( remaining * t ) + energy ) / ( remaining + incoming )
				: t;
			if( newTotal > 0.0f && conduction > 0.0f ) {
				Direction open = connectivity[i];
				float exchange = 0.0f;
				if( ( open & Direction.East ) != 0 ) {
					exchange += temperature[i + 1] - t;
				}
				if( ( open & Direction.South ) != 0 ) {
					exchange += temperature[i + stride] - t;
				}
				if( ( open & Direction.West ) != 0 ) {
					exchange += temperature[i - 1] - t;
				}
				if( ( open & Direction.North ) != 0 ) {
					exchange += temperature[i - stride] - t;
				}
				nextTemperature += conduction * exchange;
			}
			temperatureNext[i] = nextTemperature;

			for( int g = 0; g < _gas.Length; g++ ) {
				IGridLayer<float>? layer = _condensate[g];
				if( layer is null ) {
					continue;
				}
				float point = _gases.GetDefinition( new GasIndex( g ) ).CondensationPoint!.Value;
				ref float gas = ref _gasNext[g].Cells[i];
				ref float condensate = ref layer.Cells[i];
				float phase = 0.0f;
				if( nextTemperature < point ) {
					phase = gas * condensation;
				} else if( nextTemperature > point ) {
					phase = -condensate * condensation;
				}
				if( phase != 0.0f ) {
					gas -= phase;
					condensate += phase;
					layer.MarkDirty( slot );
					if( MathF.Abs( phase ) > Epsilon ) {
						changing = true;
					}
				}
			}

			_vented += vented;
			if( MathF.Abs( newTotal - own ) > Epsilon
				|| MathF.Abs( nextTemperature - t ) > Epsilon
				|| MathF.Abs( flowEast[i] ) > Epsilon
				|| MathF.Abs( flowSouth[i] ) > Epsilon
				|| vent[i] > Epsilon
			) {
				changing = true;
			}
		}

		return changing;
	}

	private void Publish() {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		int stride = _flowEast.Stride;
		float windScale = _settings.WindScale * 0.5f;
		Span<float> pressure = _pressure.Cells;
		Span<float> temperature = _temperature.Cells;
		Span<float> flowEast = _flowEast.Cells;
		Span<float> flowSouth = _flowSouth.Cells;
		Span<float> windX = _windX.Cells;
		Span<float> windY = _windY.Cells;

		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			_flowEast.ExchangeHalo( slot );
			_flowSouth.ExchangeHalo( slot );
		}
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			ReadOnlySpan<ulong> mask = layout.GetValidityMask( slot );
			for( int local = 0; local < ChunkSize * ChunkSize; local++ ) {
				if( !IsValid( mask, local ) ) {
					continue;
				}
				int i = ToIndex( slot, local );
				float total = GetTotal( i );
				pressure[i] = total * temperature[i] / ReferenceTemperature;
				if( total <= 0.0f ) {
					windX[i] = 0.0f;
					windY[i] = 0.0f;
					continue;
				}
				float vx = ( flowEast[i - 1] + flowEast[i] ) * 0.5f / total;
				float vy = ( flowSouth[i - stride] + flowSouth[i] ) * 0.5f / total;
				float speed = MathF.Sqrt( ( vx * vx ) + ( vy * vy ) );
				float dynamic = windScale * total * speed;
				windX[i] = dynamic * vx;
				windY[i] = dynamic * vy;
			}
		}
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
	}

	private static bool IsValid(
		ReadOnlySpan<ulong> mask,
		int local
	) {
		return ( ( mask[local >> 6] >> ( local & 63 ) ) & 1UL ) != 0;
	}

}
