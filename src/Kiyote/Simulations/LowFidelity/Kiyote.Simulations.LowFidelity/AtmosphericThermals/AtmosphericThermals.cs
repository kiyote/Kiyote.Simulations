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
	private readonly IAtmosphere _atmosphere;
	private readonly IThermal _thermal;
	private readonly float[] _conductances;
	private readonly float[] _capacities;
	private readonly int _ticksPerExchange;
	private readonly float _exchangeTime;
	private ConvectionCell[] _cells;
	private ConvectionLink[] _links;
	private bool _topologyChanged;
	private int _ticks;
	private bool _disposed;

	public AtmosphericThermals(
		ICompiledGridAssembly<TCell> compiled,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy,
		IGridAtmospherics atmospherics,
		IGridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		IAtmosphericThermalsSettings settings,
		ISimulationClock clock,
		IAtmosphericThermalsConvection convection
	) {
		_compiled = compiled;
		_atmosphereStrategy = atmosphereStrategy;
		_thermalStrategy = thermalStrategy;
		_atmosphericsSettings = atmosphericsSettings;
		_convection = convection;
		_conductances = [.. materials.Definitions.Select( d => d.Convection )];
		_capacities = [.. materials.Definitions.Select( d => d.HeatCapacity )];
		_ticksPerExchange = Math.Max( 1, (int)MathF.Round( settings.ExchangeInterval / clock.FixedTimeStep ) );
		_exchangeTime = _ticksPerExchange * clock.FixedTimeStep;
		_cells = [];
		_links = [];
		_atmosphere = atmospherics.Create( compiled, atmosphereStrategy );
		bool initialized = false;
		try {
			_thermal = thermals.Create( compiled, thermalStrategy );
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

	// Both simulations step on the same clock, so the atmosphere's tick count drives the exchanges.
	int IAtmosphericThermals.Update(
		TimeSpan elapsed
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );

		_ticks += _atmosphere.Update( elapsed );
		_ = _thermal.Update( elapsed );
		if( _topologyChanged ) {
			BuildLinks();
		}

		int exchanges = 0;
		while( _ticks >= _ticksPerExchange ) {
			_convection.Exchange( _cells, _links, _exchangeTime, _atmosphericsSettings.HeatCapacity, _atmosphere, _thermal );
			_ticks -= _ticksPerExchange;
			exchanges++;
		}
		return exchanges;
	}

	void IAtmosphericThermals.InvalidateTopology(
		Rect area
	) {
		_atmosphere.InvalidateTopology( area );
		_thermal.InvalidateTopology( area );
		_topologyChanged = true;
	}

	void IAtmosphericThermals.InvalidateTopology() {
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
	// (occupied and impermeable). Only changes with the topology.
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
