using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class Thermal<TCell, TStrategy> : IThermal
	where TStrategy : struct, IThermalCellStrategy<TCell> {

	private readonly IGridAssembly<TCell> _assembly;
	private readonly TStrategy _strategy;
	private readonly IMaterialRegistry _materials;
	private readonly IThermalsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IGridCompiler _compiler;
	private readonly IThermalConduction _conduction;
	private readonly IThermalRadiation _radiation;
	private readonly IThermalSolar _solar;
	private readonly float[] _conductivities;
	private readonly float[] _capacities;
	private readonly float[] _emissivities;
	private readonly float[] _absorptivities;
	private readonly float[] _temperaturePadded;
	private readonly float[] _conductivityPadded;
	private readonly float[] _capacityPadded;
	private Vector _sunDirection;
	private bool _topologyChanged;
	private bool _solarStale;
	private float _accumulator;
	private bool _disposed;

	private ICompiledGridAssembly<TCell> _compiled;
	private IGridLayer<float> _temperature;
	private IGridLayer<float> _temperatureNext;
	private IGridLayer<float> _conductivity;
	private IGridLayer<float> _capacity;
	private IGridLayer<float> _emissivity;
	private IGridLayer<float> _absorptivity;
	private IGridLayer<float> _solarPower;
	private IGridLayer<Direction> _vacuum;

	public Thermal(
		IGridAssembly<TCell> assembly,
		TStrategy strategy,
		IMaterialRegistry materials,
		IThermalsSettings settings,
		ISimulationClock clock,
		IGridCompiler compiler,
		IThermalConduction conduction,
		IThermalRadiation radiation,
		IThermalSolar solar
	) {
		_assembly = assembly;
		_strategy = strategy;
		_materials = materials;
		_settings = settings;
		_clock = clock;
		_compiler = compiler;
		_conduction = conduction;
		_radiation = radiation;
		_solar = solar;
		_conductivities = [.. materials.Definitions.Select( d => d.Conductivity )];
		_capacities = [.. materials.Definitions.Select( d => d.HeatCapacity )];
		_emissivities = [.. materials.Definitions.Select( d => d.Emissivity )];
		_absorptivities = [.. materials.Definitions.Select( d => d.Absorptivity )];
		_temperaturePadded = new float[PaddedArea];
		_conductivityPadded = new float[PaddedArea];
		_capacityPadded = new float[PaddedArea];
		Compile();
	}

	IMaterialRegistry IThermal.Materials => _materials;

	IGridLayer<float> IThermal.Temperature => _temperature;

	Vector IThermal.SunDirection {
		get => _sunDirection;
		set {
			_sunDirection = value;
			_solarStale = true;
		}
	}

	int IThermal.Update(
		TimeSpan elapsed
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );

		if( _compiled.IsStale ) {
			_compiled.Commit();
			_compiled.Dispose();
			Compile();
		}
		if( _topologyChanged ) {
			RebuildStructure();
		}
		if( _solarStale ) {
			_solar.Illuminate( _compiled.ChunkLayout, _sunDirection, _settings.SolarFlux, _absorptivity, _vacuum, _solarPower );
			_solarStale = false;
		}

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
		return steps;
	}

	void IThermal.AddEnergy(
		int column,
		int row,
		float joules
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( !_compiled.ChunkLayout.TryLocate( column, row, out int chunk, out int local ) ) {
			return;
		}
		float capacity = _capacity.GetValues( chunk )[local];
		if( capacity <= 0.0f ) {
			return;
		}
		ref float t = ref _temperature.GetValues( chunk )[local];
		t = MathF.Max( 0.0f, t + ( joules / capacity ) );
		_temperature.MarkDirty( chunk );
	}

	void IThermal.Commit() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_compiled.Commit();
	}

	void IThermal.InvalidateTopology(
		Rect area
	) {
		_topologyChanged = true;
	}

	void IThermal.InvalidateTopology() {
		_topologyChanged = true;
	}

	void IDisposable.Dispose() {
		if( _disposed ) {
			return;
		}
		_compiled.Dispose();
		_disposed = true;
	}

	[System.Diagnostics.CodeAnalysis.MemberNotNull(
		nameof( _compiled ), nameof( _temperature ), nameof( _temperatureNext ), nameof( _conductivity ),
		nameof( _capacity ), nameof( _emissivity ), nameof( _absorptivity ), nameof( _solarPower ), nameof( _vacuum )
	)]
	private void Compile() {
		_compiled = _compiler.Compile( _assembly, ChunkSize );
		_temperature = _compiled.Bind<float, ThermalTemperatureBinding<TCell, TStrategy>>( new ThermalTemperatureBinding<TCell, TStrategy>( _strategy ) );
		_temperatureNext = _compiled.CreateLayer<float>();
		_solarPower = _compiled.CreateLayer<float>();
		BindStructure();
		_topologyChanged = false;
		_solarStale = true;
	}

	// Material properties and vacuum faces only change with the topology.
	private void RebuildStructure() {
		_compiled.Commit();
		_compiled.RemoveLayer( _conductivity );
		_compiled.RemoveLayer( _capacity );
		_compiled.RemoveLayer( _emissivity );
		_compiled.RemoveLayer( _absorptivity );
		_compiled.RemoveLayer( _vacuum );
		BindStructure();
		_topologyChanged = false;
		_solarStale = true;
	}

	[System.Diagnostics.CodeAnalysis.MemberNotNull(
		nameof( _conductivity ), nameof( _capacity ), nameof( _emissivity ), nameof( _absorptivity ), nameof( _vacuum )
	)]
	private void BindStructure() {
		_conductivity = BindProperty( _conductivities );
		_capacity = BindProperty( _capacities );
		_emissivity = BindProperty( _emissivities );
		_absorptivity = BindProperty( _absorptivities );
		_vacuum = _compiled.CreateVacuumLayer();
	}

	private IGridLayer<float> BindProperty(
		float[] values
	) {
		return _compiled.Bind<float, MaterialPropertyBinding<TCell, TStrategy>>( new MaterialPropertyBinding<TCell, TStrategy>( _strategy, values ) );
	}

	// Every non-empty chunk writes every cell of temperatureNext, so the layers can be swapped
	// rather than copied back. Empty chunks hold no cells, so their values never matter.
	private void Step(
		float dt
	) {
		GridLayout layout = _compiled.ChunkLayout;
		foreach( int chunk in layout.EnumerateChunks() ) {
			if( IsEmpty( layout, chunk ) ) {
				continue;
			}
			ReadOnlySpan<ulong> presence = layout.GetPresence( chunk );
			Span<float> next = _temperatureNext.GetValues( chunk );
			Gather( _temperature, chunk, _temperaturePadded );
			Gather( _conductivity, chunk, _conductivityPadded );
			Gather( _capacity, chunk, _capacityPadded );
			_conduction.Conduct( presence, dt, _temperaturePadded, _conductivityPadded, _capacityPadded, next );
			_radiation.Radiate(
				presence,
				dt,
				_settings.StefanBoltzmann,
				_settings.SpaceTemperature,
				_emissivity.GetValues( chunk ),
				_capacity.GetValues( chunk ),
				_solarPower.GetValues( chunk ),
				_vacuum.GetValues( chunk ),
				next
			);
		}

		_compiled.Swap( _temperature, _temperatureNext );
		foreach( int chunk in layout.EnumerateChunks() ) {
			if( !IsEmpty( layout, chunk ) ) {
				_temperature.MarkDirty( chunk );
			}
		}
	}

}
