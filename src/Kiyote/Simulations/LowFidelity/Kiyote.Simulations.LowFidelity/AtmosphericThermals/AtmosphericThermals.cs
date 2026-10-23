using System.Numerics;
using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class AtmosphericThermals<TCell, TAtmosphereStrategy, TThermalStrategy> : IAtmosphericThermals
	where TAtmosphereStrategy : struct, IAtmosphereCellStrategy<TCell>
	where TThermalStrategy : struct, IThermalCellStrategy<TCell> {

	private static readonly int NorthNeighbour = Direction.North.ToNeighbourIndex();
	private static readonly int EastNeighbour = Direction.East.ToNeighbourIndex();
	private static readonly int SouthNeighbour = Direction.South.ToNeighbourIndex();
	private static readonly int WestNeighbour = Direction.West.ToNeighbourIndex();

	private readonly ICompiledGridAssembly<TCell> _compiled;
	private readonly TAtmosphereStrategy _atmosphereStrategy;
	private readonly TThermalStrategy _thermalStrategy;
	private readonly IAtmosphericsSettings _atmosphericsSettings;
	private readonly IAtmosphericThermalsConvection _convection;
	private readonly Atmosphere<TCell, TAtmosphereStrategy> _atmosphere;
	private readonly Thermal<TCell, TThermalStrategy> _thermal;
	private readonly float[] _conductances;
	private readonly float[] _capacities;
	private readonly int _ticksPerExchange;
	private readonly float _exchangeTime;
	private readonly float _fixedTimeStep;
	private readonly long _fixedStepTicks;
	private readonly int _maxStepsPerUpdate;
	private ConvectionCell[] _cells;
	private ConvectionLink[] _links;
	private bool _topologyChanged;
	private int _ticks;
	private long _accumulatorTicks;
	private bool _disposed;

	public AtmosphericThermals(
		ICompiledGridAssembly<TCell> compiled,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy,
		GridAtmospherics atmospherics,
		GridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		IAtmosphericThermalsSettings settings,
		ISimulationClock clock,
		IAtmosphericThermalsConvection convection
	) {
		_fixedTimeStep = clock.FixedTimeStep;
		if( !float.IsFinite( _fixedTimeStep ) || _fixedTimeStep <= 0.0f || _fixedTimeStep > TimeSpan.MaxValue.TotalSeconds ) {
			throw new ArgumentOutOfRangeException( nameof( clock ), "The fixed step must be finite, positive and representable as a TimeSpan." );
		}
		_fixedStepTicks = TimeSpan.FromSeconds( _fixedTimeStep ).Ticks;
		if( _fixedStepTicks == 0 ) {
			throw new ArgumentOutOfRangeException( nameof( clock ), "The fixed step must be at least one TimeSpan tick." );
		}
		float interval = settings.ExchangeInterval;
		float ticks = MathF.Max( 1.0f, MathF.Round( interval / _fixedTimeStep ) );
		if( !float.IsFinite( interval ) || !float.IsFinite( ticks ) || (double)ticks > int.MaxValue ) {
			throw new ArgumentOutOfRangeException( nameof( settings ), "The exchange interval must be finite and quantize to a representable number of fixed steps." );
		}
		_ticksPerExchange = (int)ticks;
		_exchangeTime = _ticksPerExchange * _fixedTimeStep;
		if( !float.IsFinite( _exchangeTime ) ) {
			throw new ArgumentOutOfRangeException( nameof( settings ), "The quantized exchange duration must be finite." );
		}
		_compiled = compiled;
		_atmosphereStrategy = atmosphereStrategy;
		_thermalStrategy = thermalStrategy;
		_atmosphericsSettings = atmosphericsSettings;
		_convection = convection;
		_conductances = [.. materials.Definitions.Select( d => d.Convection )];
		_capacities = [.. materials.Definitions.Select( d => d.HeatCapacity )];
		_cells = [];
		_links = [];
		_atmosphere = atmospherics.Create( compiled, atmosphereStrategy );
		bool initialized = false;
		try {
			_thermal = thermals.Create( compiled, thermalStrategy );
			int atmosphereCap = _atmosphere.MaxStepsPerUpdate;
			int thermalCap = _thermal.MaxStepsPerUpdate;
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero( atmosphereCap, nameof( IAtmosphericsSettings.MaxStepsPerUpdate ) );
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero( thermalCap, nameof( IThermalsSettings.MaxStepsPerUpdate ) );
			_maxStepsPerUpdate = Math.Min( atmosphereCap, thermalCap );
			BuildLinks();
			_atmosphere.Publish( ( (IThermalState)_thermal ).Temperature );
			initialized = true;
		} finally {
			if( !initialized ) {
				_thermal?.Dispose();
				_atmosphere.Dispose();
			}
		}
	}

	IAtmosphereState IAtmosphericThermals.Atmosphere => _atmosphere;

	IThermalState IAtmosphericThermals.Thermal => _thermal;

	IAtmosphereFrames IAtmosphericThermals.Frames => ( (IAtmosphereState)_atmosphere ).Frames;

	int IAtmosphericThermals.Update(
		TimeSpan elapsed
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		ArgumentOutOfRangeException.ThrowIfNegative( elapsed.Ticks, nameof( elapsed ) );

		_atmosphere.PrepareUpdate();
		_thermal.PrepareUpdate();
		if( _topologyChanged ) {
			BuildLinks();
		}

		// Split before adding so even TimeSpan.MaxValue cannot overflow the accumulator.
		long available = elapsed.Ticks / _fixedStepTicks;
		long remainder = elapsed.Ticks % _fixedStepTicks;
		long untilNext = _fixedStepTicks - _accumulatorTicks;
		if( remainder >= untilNext ) {
			available++;
			remainder -= untilNext;
		} else {
			remainder += _accumulatorTicks;
		}
		int steps;
		if( available > _maxStepsPerUpdate ) {
			steps = _maxStepsPerUpdate;
			_accumulatorTicks = 0;
		} else {
			steps = (int)available;
			_accumulatorTicks = remainder;
		}

		int exchanges = 0;
		for( int i = 0; i < steps; i++ ) {
			_atmosphere.Step( _fixedTimeStep );
			_thermal.Step( _fixedTimeStep );
			_ticks++;
			if( _ticks >= _ticksPerExchange ) {
				_convection.Exchange( _cells, _links, _exchangeTime, _atmosphericsSettings.HeatCapacity, _atmosphere, _thermal );
				_ticks -= _ticksPerExchange;
				exchanges++;
			}
		}
		_atmosphere.Publish( ( (IThermalState)_thermal ).Temperature );
		return exchanges;
	}

	void IAtmosphericThermals.InvalidateTopology(
		Rect area
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( area.IsEmpty ) {
			return;
		}
		_atmosphere.InvalidateTopology( area );
		_thermal.InvalidateTopology( area );
		_topologyChanged = true;
	}

	void IAtmosphericThermals.InvalidateTopology() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_atmosphere.InvalidateTopology();
		_thermal.InvalidateTopology();
		_topologyChanged = true;
	}

	void IDisposable.Dispose() {
		if( _disposed ) {
			return;
		}
		_atmosphere.Dispose();
		_thermal.Dispose();
		_disposed = true;
	}

	// Pairs every gas cell (occupied and permeable) with its own floor and each adjacent wall
	// (occupied and impermeable), in chunk/local presence order, then floor, N/E/S/W face order.
	// Refresh after occupancy, permeability or material invalidation.
	private void BuildLinks() {
		using OwnedGridLayers<TCell> layers = new OwnedGridLayers<TCell>( _compiled, ChunkSize );
		IGridLayer<bool> permeable = layers.Add( _compiled.Bind<bool, PermeabilityBinding<TCell, TAtmosphereStrategy>>( new PermeabilityBinding<TCell, TAtmosphereStrategy>( _atmosphereStrategy ) ) );
		IGridLayer<float> conductance = layers.Add( _compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _conductances ) ) );
		IGridLayer<float> capacity = layers.Add( _compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _capacities ) ) );
		GridLayout layout = _compiled.ChunkLayout;

		List<ConvectionCell> cells = [];
		List<ConvectionLink> links = [];
		foreach( int chunk in layout.EnumerateChunks() ) {
			if( layout.GetPopulatedCount( chunk ) == 0 ) {
				continue;
			}
			ReadOnlySpan<int> neighbours = layout.GetNeighbours( chunk );
			LinkChunk centre = new LinkChunk( chunk, layout, permeable, conductance, capacity );
			LinkChunk north = new LinkChunk( neighbours[NorthNeighbour], layout, permeable, conductance, capacity );
			LinkChunk east = new LinkChunk( neighbours[EastNeighbour], layout, permeable, conductance, capacity );
			LinkChunk south = new LinkChunk( neighbours[SouthNeighbour], layout, permeable, conductance, capacity );
			LinkChunk west = new LinkChunk( neighbours[WestNeighbour], layout, permeable, conductance, capacity );
			for( int row = 0; row < ChunkSize; row++ ) {
				uint mask = GetRowMask( centre.Presence, row );
				while( mask != 0 ) {
					int column = BitOperations.TrailingZeroCount( mask );
					mask &= mask - 1;
					int local = ChunkIndex( column, row );
					if( !centre.Permeable[local] ) {
						continue;
					}
					int first = links.Count;
					links.Add( new ConvectionLink( chunk, local, centre.Conductance[local], centre.Capacity[local] ) );
					AddWall( centre, north, row == 0, ( local - ChunkSize ) & ( ChunkArea - 1 ), links );
					AddWall( centre, east, column == ChunkSize - 1, ChunkIndex( ( column + 1 ) & ( ChunkSize - 1 ), row ), links );
					AddWall( centre, south, row == ChunkSize - 1, ( local + ChunkSize ) & ( ChunkArea - 1 ), links );
					AddWall( centre, west, column == 0, ChunkIndex( ( column - 1 ) & ( ChunkSize - 1 ), row ), links );
					cells.Add( new ConvectionCell( chunk, local, first, links.Count - first ) );
				}
			}
		}
		_cells = [.. cells];
		_links = [.. links];
		_topologyChanged = false;
	}

	private static void AddWall(
		in LinkChunk centre,
		in LinkChunk adjacent,
		bool crossesChunk,
		int local,
		List<ConvectionLink> links
	) {
		ref readonly LinkChunk target = ref crossesChunk ? ref adjacent : ref centre;
		if( target.Chunk < 0
			|| ( GetRowMask( target.Presence, local / ChunkSize ) & ( 1u << ( local & ( ChunkSize - 1 ) ) ) ) == 0
			|| target.Permeable[local] ) {
			return;
		}
		links.Add( new ConvectionLink( target.Chunk, local, target.Conductance[local], target.Capacity[local] ) );
	}

	private readonly ref struct LinkChunk {
		public int Chunk { get; }
		public ReadOnlySpan<ulong> Presence { get; }
		public ReadOnlySpan<bool> Permeable { get; }
		public ReadOnlySpan<float> Conductance { get; }
		public ReadOnlySpan<float> Capacity { get; }

		public LinkChunk( int chunk, GridLayout layout, IGridLayer<bool> permeable, IGridLayer<float> conductance, IGridLayer<float> capacity ) {
			Chunk = chunk;
			Presence = chunk < 0 ? [] : layout.GetPresence( chunk );
			Permeable = chunk < 0 ? [] : permeable.GetValues( chunk );
			Conductance = chunk < 0 ? [] : conductance.GetValues( chunk );
			Capacity = chunk < 0 ? [] : capacity.GetValues( chunk );
		}
	}

}
