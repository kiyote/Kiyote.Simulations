namespace Kiyote.Simulations.LowFidelity.Atmospherics;

// Disposing any copy releases this acquisition. Stale disposal cannot release a later lease.
// Do not use a lease, its layer views or spans after any copy has been disposed.
public readonly struct AtmosphereFrameLease : IDisposable, IEquatable<AtmosphereFrameLease> {

	private readonly AtmosphereFrames _owner;
	private readonly AtmosphereFrameBuffer _buffer;
	private readonly long _token;

	internal AtmosphereFrameLease(
		AtmosphereFrames owner,
		AtmosphereFrameBuffer buffer,
		long token
	) {
		_owner = owner;
		_buffer = buffer;
		_token = token;
	}

	public IGasRegistry Gases => _owner.Gases;

	// Fixed physics steps, not publications: zero-step updates can change layers without advancing it.
	public long StepCount => _buffer.StepCount;

	public float Vented => _buffer.Vented;

	public SimulationFrameLayer Pressure => new SimulationFrameLayer( _buffer.Pressure );

	public SimulationFrameLayer Temperature => new SimulationFrameLayer( _buffer.Temperature );

	public SimulationFrameLayer WindX => new SimulationFrameLayer( _buffer.WindX );

	public SimulationFrameLayer WindY => new SimulationFrameLayer( _buffer.WindY );

	public bool HasStructuralTemperature => _buffer.StructuralTemperature is not null;

	public SimulationFrameLayer StructuralTemperature => new SimulationFrameLayer(
		_buffer.StructuralTemperature
			?? throw new InvalidOperationException( "Structural temperature is only available in coupled simulation frames." )
	);

	public SimulationFrameLayer GetGas(
		GasIndex gas
	) {
		ValidateGas( gas );
		return new SimulationFrameLayer( _buffer.Gas[gas.Value] );
	}

	public SimulationFrameLayer GetCondensate(
		GasIndex gas
	) {
		ValidateGas( gas );
		return new SimulationFrameLayer(
			_buffer.Condensate[gas.Value]
				?? throw new ArgumentException( $"Gas '{Gases.GetDefinition( gas ).Id}' does not condense.", nameof( gas ) )
		);
	}

	public void Dispose() {
		_owner?.Release( _token );
	}

	public bool Equals(
		AtmosphereFrameLease other
	) {
		return ReferenceEquals( _owner, other._owner ) && _token == other._token;
	}

	public override bool Equals(
		object? obj
	) {
		return obj is AtmosphereFrameLease other && Equals( other );
	}

	public override int GetHashCode() {
		return HashCode.Combine( _owner, _token );
	}

	public static bool operator ==( AtmosphereFrameLease left, AtmosphereFrameLease right ) {
		return left.Equals( right );
	}

	public static bool operator !=( AtmosphereFrameLease left, AtmosphereFrameLease right ) {
		return !left.Equals( right );
	}

	private void ValidateGas(
		GasIndex gas
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( gas.Value, nameof( gas ) );
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( gas.Value, _buffer.Gas.Length, nameof( gas ) );
	}

}
