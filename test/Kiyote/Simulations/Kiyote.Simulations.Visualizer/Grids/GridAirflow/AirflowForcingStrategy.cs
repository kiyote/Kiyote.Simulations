using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridAirflow;

// Converts a pressure difference between two adjacent cells into a Velocity
// impulse pointed from the higher-pressure cell toward its lower-pressure
// neighbor, scaled by gain - a simple -gradient(P) body force.
[ExcludeFromCodeCoverage]
internal readonly struct AirflowForcingStrategy : IPressureForcingStrategy {

	private readonly double _gain;
	private readonly double _maxSpeed;

	public AirflowForcingStrategy(
		double gain,
		double maxSpeed
	) {
		_gain = gain;
		_maxSpeed = maxSpeed;
	}

	public Velocity CalculateForce(
		GridCell<double> source,
		GridCell<double> destination
	) {
		double deltaColumn = destination.Column - source.Column;
		double deltaRow = destination.Row - source.Row;
		double difference = source.Cell - destination.Cell;
		double column = deltaColumn * difference * _gain;
		double row = deltaRow * difference * _gain;

		// Without a speed clamp, a large pressure differential can produce a
		// velocity impulse that moves "air" more than one cell per timestep,
		// violating the CFL condition the semi-Lagrangian advect/project steps
		// rely on for stability - this manifests as noisy, streaky artifacts
		// instead of a smooth, converging flow.
		double speed = Math.Sqrt( ( column * column ) + ( row * row ) );
		if( speed > _maxSpeed && speed > 0.0 ) {
			double scale = _maxSpeed / speed;
			column *= scale;
			row *= scale;
		}

		return new Velocity( column, row );
	}

	public Velocity Combine(
		Velocity left,
		Velocity right
	) {
		return left + right;
	}

}
