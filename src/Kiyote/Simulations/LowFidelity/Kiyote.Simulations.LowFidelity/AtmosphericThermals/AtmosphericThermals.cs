using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using Kiyote.Simulations.LowFidelity.Atmospherics;
using Kiyote.Simulations.LowFidelity.Thermals;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.AtmosphericThermals;

internal sealed class AtmosphericThermals<TCell, TAtmosphereStrategy, TThermalStrategy> : IAtmosphericThermals
	where TAtmosphereStrategy : struct, IAtmosphereCellStrategy<TCell>
	where TThermalStrategy : struct, IThermalCellStrategy<TCell> {

	private const int Halo = 0;

	private static readonly (int Column, int Row)[] Faces = [( 0, -1 ), ( 1, 0 ), ( 0, 1 ), ( -1, 0 )];

	private readonly IGridAssembly<TCell> _assembly;
	private readonly TAtmosphereStrategy _atmosphereStrategy;
	private readonly TThermalStrategy _thermalStrategy;
	private readonly IAtmosphericsSettings _atmosphericsSettings;
	private readonly IGridCompiler _compiler;
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
		IGridAssembly<TCell> assembly,
		TAtmosphereStrategy atmosphereStrategy,
		TThermalStrategy thermalStrategy,
		IGridAtmospherics atmospherics,
		IGridThermals thermals,
		IMaterialRegistry materials,
		IAtmosphericsSettings atmosphericsSettings,
		IAtmosphericThermalsSettings settings,
		ISimulationClock clock,
		IGridCompiler compiler,
		IAtmosphericThermalsConvection convection
	) {
		_assembly = assembly;
		_atmosphereStrategy = atmosphereStrategy;
		_thermalStrategy = thermalStrategy;
		_atmosphericsSettings = atmosphericsSettings;
		_compiler = compiler;
		_convection = convection;
		_conductances = [.. materials.Definitions.Select( d => d.Convection )];
		_capacities = [.. materials.Definitions.Select( d => d.HeatCapacity )];
		_ticksPerExchange = Math.Max( 1, (int)MathF.Round( settings.ExchangeInterval / clock.FixedTimeStep ) );
		_exchangeTime = _ticksPerExchange * clock.FixedTimeStep;
		_atmosphere = atmospherics.Create( assembly, atmosphereStrategy );
		_thermal = thermals.Create( assembly, thermalStrategy );
		_cells = [];
		_links = [];
		BuildLinks();
	}

	IAtmosphere IAtmosphericThermals.Atmosphere => _atmosphere;

	IThermal IAtmosphericThermals.Thermal => _thermal;

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

	void IAtmosphericThermals.Commit() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_atmosphere.Commit();
		_thermal.Commit();
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
		using ICompiledGridAssembly<TCell> compiled = _compiler.Compile( _assembly, ChunkSize );
		IGridLayer<bool> permeable = compiled.Bind<bool, PermeabilityBinding<TCell, TAtmosphereStrategy>>( new PermeabilityBinding<TCell, TAtmosphereStrategy>( _atmosphereStrategy ), Halo );
		IGridLayer<float> conductance = compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _conductances ), Halo );
		IGridLayer<float> capacity = compiled.Bind<float, MaterialPropertyBinding<TCell, TThermalStrategy>>( new MaterialPropertyBinding<TCell, TThermalStrategy>( _thermalStrategy, _capacities ), Halo );
		IGridChunkLayout layout = compiled.ChunkLayout;

		List<ConvectionCell> cells = [];
		List<ConvectionLink> links = [];
		Rect? bounds = layout.Bounds;
		if( bounds.HasValue ) {
			Rect area = bounds.Value;
			for( int row = area.Y1; row <= area.Y2; row++ ) {
				for( int column = area.X1; column <= area.X2; column++ ) {
					if( !layout.TryGetCell( column, row, out _, out _ ) || !permeable[column, row] ) {
						continue;
					}
					int first = links.Count;
					links.Add( new ConvectionLink( column, row, conductance[column, row], capacity[column, row] ) );
					foreach( (int dc, int dr) in Faces ) {
						int c = column + dc;
						int r = row + dr;
						if( layout.TryGetCell( c, r, out _, out _ ) && !permeable[c, r] ) {
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
