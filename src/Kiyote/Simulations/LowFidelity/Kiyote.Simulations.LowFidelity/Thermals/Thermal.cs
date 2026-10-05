using Kiyote.Geometry;
using Kiyote.Geometry.Topology;
using static Kiyote.Simulations.LowFidelity.Atmospherics.AtmosphereSimd;

namespace Kiyote.Simulations.LowFidelity.Thermals;

internal sealed class Thermal<TCell, TStrategy> : IThermal
	where TStrategy : struct, IThermalCellStrategy<TCell> {

	private const int Halo = 1;

	private readonly IGridAssembly<TCell> _assembly;
	private readonly TStrategy _strategy;
	private readonly IMaterialRegistry _materials;
	private readonly IThermalsSettings _settings;
	private readonly IGridCompiler _compiler;
	private readonly IThermalConduction _conduction;
	private readonly IThermalRadiation _radiation;
	private readonly IThermalSolar _solar;
	private readonly float[] _conductivities;
	private readonly float[] _capacities;
	private readonly float[] _emissivities;
	private readonly float[] _absorptivities;
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
		IGridCompiler compiler,
		IThermalConduction conduction,
		IThermalRadiation radiation,
		IThermalSolar solar
	) {
		_assembly = assembly;
		_strategy = strategy;
		_materials = materials;
		_settings = settings;
		_compiler = compiler;
		_conduction = conduction;
		_radiation = radiation;
		_solar = solar;
		_conductivities = [.. materials.Definitions.Select( d => d.Conductivity )];
		_capacities = [.. materials.Definitions.Select( d => d.HeatCapacity )];
		_emissivities = [.. materials.Definitions.Select( d => d.Emissivity )];
		_absorptivities = [.. materials.Definitions.Select( d => d.Absorptivity )];
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

	int IThermal.Advance(
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

		float dt = _settings.FixedTimeStep;
		_accumulator += (float)elapsed.TotalSeconds;
		int steps = (int)( _accumulator / dt );
		if( steps > _settings.MaxStepsPerAdvance ) {
			steps = _settings.MaxStepsPerAdvance;
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
		if( !TryGetIndex( column, row, out int slot, out int index ) ) {
			return;
		}
		float capacity = _capacity.Cells[index];
		if( capacity <= 0.0f ) {
			return;
		}
		ref float t = ref _temperature.Cells[index];
		t = MathF.Max( 0.0f, t + ( joules / capacity ) );
		_temperature.MarkDirty( slot );
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
		_temperature = _compiled.Bind<float, ThermalTemperatureBinding<TCell, TStrategy>>( new ThermalTemperatureBinding<TCell, TStrategy>( _strategy ), Halo );
		_temperatureNext = _compiled.CreateLayer<float>( Halo );
		_solarPower = _compiled.CreateLayer<float>( Halo );
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
		_vacuum = _compiled.CreateVacuumLayer( Halo );
	}

	private IGridLayer<float> BindProperty(
		float[] values
	) {
		return _compiled.Bind<float, MaterialPropertyBinding<TCell, TStrategy>>( new MaterialPropertyBinding<TCell, TStrategy>( _strategy, values ), Halo );
	}

	private void Step(
		float dt
	) {
		IGridChunkLayout layout = _compiled.ChunkLayout;
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) != ChunkState.Empty ) {
				_temperature.ExchangeHalo( slot );
				_capacity.ExchangeHalo( slot );
				_conductivity.ExchangeHalo( slot );
			}
		}
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			ReadOnlySpan<ulong> mask = layout.GetValidityMask( slot );
			_conduction.Conduct( slot, mask, dt, _temperature, _conductivity, _capacity, _temperatureNext );
			_radiation.Radiate( slot, mask, dt, _settings.StefanBoltzmann, _settings.SpaceTemperature, _emissivity, _capacity, _solarPower, _vacuum, _temperatureNext );
		}
		for( int slot = 0; slot < layout.SlotCount; slot++ ) {
			if( layout.GetState( slot ) == ChunkState.Empty ) {
				continue;
			}
			_temperatureNext.GetChunk( slot ).CopyTo( _temperature.GetChunk( slot ) );
			_temperature.MarkDirty( slot );
		}
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
		index = _temperature.IndexOf( slot, local % ChunkSize, local / ChunkSize );
		return true;
	}

}
