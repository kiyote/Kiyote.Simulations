using System.Diagnostics;

namespace Kiyote.Simulations.Manager;

internal class SimulationManager : ISimulationManager {

	private readonly float _timeStep;
	private readonly double _maximumScaledDeltaTime;


	private long _lastTimestamp;
	private bool _running;
	private double _accumulator;

	public SimulationManager(
		SimulationManagerOptions options
	) {
		_timeStep = (float)options.FixedTimeStep.TotalSeconds;
		_maximumScaledDeltaTime = options.MaximumScaledDeltaTime.TotalSeconds;
		_running = false;
		_accumulator = 0.0;
	}

	public bool Running {
		get {
			return _running;
		}
		set {
			if( _running != value ) {
				_running = value;
				// If we're toggling the simulation to start running, grab
				// the current timestamp so we can calculate the delta time
				// in the Update method.
				if( value ) {
					_lastTimestamp = Stopwatch.GetTimestamp();
				}
			}
		}
	}

	public void Update(
		double timeScale
	) {

		if( !Running ) {
			return;
		}

		long currentTimestamp = Stopwatch.GetTimestamp();
		TimeSpan elapsedTime = Stopwatch.GetElapsedTime( _lastTimestamp, currentTimestamp );
		_lastTimestamp = currentTimestamp;

		double deltaTime = elapsedTime.TotalSeconds;
		double scaledDeltaTime = deltaTime * timeScale;
		if (scaledDeltaTime > _maximumScaledDeltaTime ) {
			scaledDeltaTime = _maximumScaledDeltaTime;
		}

		_accumulator += scaledDeltaTime;

		while (_accumulator >= _timeStep ) {
			Tick( _timeStep );
			_accumulator -= _timeStep;
		}
	}

	private void Tick(
		float deltaTime
	) {
		throw new NotImplementedException();
	}
}
