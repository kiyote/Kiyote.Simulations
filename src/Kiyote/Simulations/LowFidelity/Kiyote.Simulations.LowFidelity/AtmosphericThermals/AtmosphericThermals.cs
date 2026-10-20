using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class AtmosphericThermals<TCell, TAtmosphereStrategy, TThermalStrategy> : IAtmosphericThermals
	where TAtmosphereStrategy : struct, IAtmosphereCellStrategy<TCell>
	where TThermalStrategy : struct, IThermalCellStrategy<TCell> {

	private static readonly (int Column, int Row)[] Faces = [( 0, -1 ), ( 1, 0 ), ( 0, 1 ), ( -1, 0 )];

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
		_atmosphere.Publish();
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
	// (occupied and impermeable). Refresh after occupancy, permeability or material invalidation.
	private void BuildLinks() {
		using OwnedGridLayers<TCell> layers = new OwnedGridLayers<TCell>( _compiled, ChunkSize );
		IGridLayer<bool> permeable = layers.Add( _compiled.Bind<bool, PermeabilityBinding<TCell, TAtmosphereStrategy>>( new PermeabilityBinding<TCell, TAtmosphereStrategy>( _atmosphereStrategy ) ) );
		IGridLayer<float> conductance = layers.Add( _compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _conductances ) ) );
		IGridLayer<float> capacity = layers.Add( _compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _capacities ) ) );
		GridLayout layout = _compiled.ChunkLayout;

		List<ConvectionCell> cells = [];
		List<ConvectionLink> links = [];
		if( layout.TryGetPopulatedBounds( out int left, out int top, out int width, out int height ) ) {
			for( int row = top; row < top + height; row++ ) {
				for( int column = left; column < left + width; column++ ) {
					if( !layout.IsPopulated( column, row ) || !permeable[column, row] ) {
						continue;
					}
					int first = links.Count;
					links.Add( new ConvectionLink( column, row, conductance[column, row], capacity[column, row] ) );
					foreach( (int dc, int dr) in Faces ) {
						int c = column + dc;
						int r = row + dr;
						if( layout.IsPopulated( c, r ) && !permeable[c, r] ) {
							links.Add( new ConvectionLink( c, r, conductance[c, r], capacity[c, r] ) );
						}
					}
					cells.Add( new ConvectionCell( column, row, first, links.Count - first ) );
				}
			}
		}
		_cells = [.. cells];
		_links = [.. links];
		_topologyChanged = false;
	}

}
