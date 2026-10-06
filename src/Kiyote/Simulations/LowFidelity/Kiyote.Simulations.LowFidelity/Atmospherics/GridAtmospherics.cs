using Kiyote.Geometry.Topology;

namespace Kiyote.Simulations.LowFidelity.Atmospherics;

internal sealed class GridAtmospherics : IGridAtmospherics {

	private readonly IGasRegistry _gases;
	private readonly IAtmosphericsSettings _settings;
	private readonly ISimulationClock _clock;
	private readonly IGridCompiler _compiler;
	private readonly IConnectivityBuilder _connectivityBuilder;
	private readonly IAtmospherePressure _pressure;
	private readonly IAtmosphereAcceleration _acceleration;
	private readonly IAtmosphereLimiter _limiter;
	private readonly IAtmosphereTransport _transport;
	private readonly IAtmosphereThermal _thermal;
	private readonly IAtmosphereCondensation _condensation;
	private readonly IAtmosphereWind _wind;

	public GridAtmospherics(
		IGasRegistry gases,
		IAtmosphericsSettings settings,
		ISimulationClock clock,
		IGridCompiler compiler,
		IConnectivityBuilder connectivityBuilder,
		IAtmospherePressure pressure,
		IAtmosphereAcceleration acceleration,
		IAtmosphereLimiter limiter,
		IAtmosphereTransport transport,
		IAtmosphereThermal thermal,
		IAtmosphereCondensation condensation,
		IAtmosphereWind wind
	) {
		_gases = gases;
		_settings = settings;
		_clock = clock;
		_compiler = compiler;
		_connectivityBuilder = connectivityBuilder;
		_pressure = pressure;
		_acceleration = acceleration;
		_limiter = limiter;
		_transport = transport;
		_thermal = thermal;
		_condensation = condensation;
		_wind = wind;
	}

	IAtmosphere IGridAtmospherics.Create<TCell, TStrategy>(
		IGridAssembly<TCell> ship,
		TStrategy strategy
	) {
		return new Atmosphere<TCell, TStrategy>(
			ship,
			strategy,
			_gases,
			_settings,
			_clock,
			_compiler,
			_connectivityBuilder,
			_pressure,
			_acceleration,
			_limiter,
			_transport,
			_thermal,
			_condensation,
			_wind
		);
	}

}
