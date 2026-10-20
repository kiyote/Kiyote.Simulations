using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class Thermal<TCell, TStrategy> : IThermal, IThermalConvectionState
	where TStrategy : struct, IThermalCellStrategy<TCell> {

	private readonly ICompiledGridAssembly<TCell> _compiled;
	private readonly OwnedGridLayers<TCell> _layers;
	private readonly TStrategy _strategy;
	private readonly IMaterialRegistry _materials;
	private readonly IThermalsSettings _settings;
	private readonly ISimulationClock _clock;
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

	private IGridLayer<float> _temperature;
	private IGridLayer<float> _temperatureNext;
	private IGridLayer<float> _conductivity;
	private IGridLayer<float> _capacity;
	private IGridLayer<float> _emissivity;
	private IGridLayer<float> _absorptivity;
	private IGridLayer<float> _solarPower;
	private IGridLayer<Direction> _vacuum;

	public Thermal(
		ICompiledGridAssembly<TCell> compiled,
		TStrategy strategy,
		IMaterialRegistry materials,
		IThermalsSettings settings,
		ISimulationClock clock,
		IThermalConduction conduction,
		IThermalRadiation radiation,
		IThermalSolar solar
	) {
		_compiled = compiled;
		_layers = new OwnedGridLayers<TCell>( compiled, ChunkSize );
		_strategy = strategy;
		_materials = materials;
		_settings = settings;
		_clock = clock;
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

	IMaterialRegistry IThermalState.Materials => _materials;

	internal int MaxStepsPerUpdate => _settings.MaxStepsPerUpdate;

	IGridLayer<float> IThermalState.Temperature => _temperature;

	IGridLayer<float> IThermalConvectionState.Temperature => _temperature;

	void IThermalConvectionState.AddEnergyAt( int chunk, int local, float capacity, float joules ) => ApplyEnergy( chunk, local, capacity, joules );

	Vector IThermalState.SunDirection {
		get => _sunDirection;
		set {
			_sunDirection = value;
			_solarStale = true;
		}
	}

	int IThermal.Update(
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
		return steps;
	}

	void IThermalState.AddEnergy(
		int column,
		int row,
		float joules
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( !_compiled.ChunkLayout.TryLocate( column, row, out int chunk, out int local ) ) {
			return;
		}
		ApplyEnergy( chunk, local, _capacity.GetValues( chunk )[local], joules );
	}

	private void ApplyEnergy(
		int chunk,
		int local,
		float capacity,
		float joules
	) {
		if( capacity <= 0.0f ) {
			return;
		}
		ref float t = ref _temperature.GetValues( chunk )[local];
		t = MathF.Max( 0.0f, t + ( joules / capacity ) );
		_temperature.MarkDirty( chunk );
	}

	public void InvalidateTopology(
		Rect area
	) {
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( !area.IsEmpty ) {
			_topologyChanged = true;
		}
	}

	public void InvalidateTopology() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_topologyChanged = true;
	}

	public void Dispose() {
		if( _disposed ) {
			return;
		}
		_layers.Dispose();
		_disposed = true;
	}

	internal void PrepareUpdate() {
		ObjectDisposedException.ThrowIf( _disposed, this );
		_layers.EnsureUsable();
		if( _topologyChanged ) {
			RebuildStructure();
		}
		if( _solarStale ) {
			_solar.Illuminate( _compiled.ChunkLayout, _sunDirection, _settings.SolarFlux, _absorptivity, _vacuum, _solarPower );
			_solarStale = false;
		}
	}

	[System.Diagnostics.CodeAnalysis.MemberNotNull(
		nameof( _temperature ), nameof( _temperatureNext ), nameof( _conductivity ),
		nameof( _capacity ), nameof( _emissivity ), nameof( _absorptivity ), nameof( _solarPower ), nameof( _vacuum )
	)]
	private void BindLayers() {
		_temperature = _layers.Add( _compiled.Bind<float, ThermalTemperatureBinding<TCell, TStrategy>>( new ThermalTemperatureBinding<TCell, TStrategy>( _strategy ) ) );
		_temperatureNext = _layers.Add( _compiled.CreateLayer<float>() );
		_solarPower = _layers.Add( _compiled.CreateLayer<float>() );
		( _conductivity, _capacity, _emissivity, _absorptivity, _vacuum ) = BindStructure( _layers );
		_topologyChanged = false;
		_solarStale = true;
	}

	private void RebuildStructure() {
		using OwnedGridLayers<TCell> replacements = new OwnedGridLayers<TCell>( _compiled, ChunkSize );
		var structure = BindStructure( replacements );
		_layers.Remove( _conductivity );
		_layers.Remove( _capacity );
		_layers.Remove( _emissivity );
		_layers.Remove( _absorptivity );
		_layers.Remove( _vacuum );
		replacements.TransferTo( _layers );
		( _conductivity, _capacity, _emissivity, _absorptivity, _vacuum ) = structure;
		_topologyChanged = false;
		_solarStale = true;
	}

	private (
		IGridLayer<float> Conductivity,
		IGridLayer<float> Capacity,
		IGridLayer<float> Emissivity,
		IGridLayer<float> Absorptivity,
		IGridLayer<Direction> Vacuum
	) BindStructure(
		OwnedGridLayers<TCell> layers
	) {
		return (
			BindProperty( layers, _conductivities ),
			BindProperty( layers, _capacities ),
			BindProperty( layers, _emissivities ),
			BindProperty( layers, _absorptivities ),
			layers.Add( _compiled.CreateVacuumLayer() )
		);
	}

	private IGridLayer<float> BindProperty(
		OwnedGridLayers<TCell> layers,
		float[] values
	) {
		return layers.Add( _compiled.Bind<float, MaterialPropertyBinding<TCell, TStrategy>>( new MaterialPropertyBinding<TCell, TStrategy>( _strategy, values ) ) );
	}

	// Every non-empty chunk writes every cell of temperatureNext, so the layers can be swapped
	// rather than copied back. Empty chunks hold no cells, so their values never matter.
	internal void Step(
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
