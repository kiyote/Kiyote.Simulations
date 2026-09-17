using System.Diagnostics.CodeAnalysis;
using Kiyote.Geometry.Grids;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridFluid;

[ExcludeFromCodeCoverage]
internal readonly struct VelocityDiffusionStrategy : IDiffusionStrategy<Velocity, Velocity> {

	private readonly double _viscosity;

	public VelocityDiffusionStrategy() : this( 1.0 ) { }

	// viscosity ranges from 0 (no flow) to 1 (fully equalizes in a single step).
	public VelocityDiffusionStrategy(
		double viscosity
	) {
		_viscosity = viscosity;
	}

	public Velocity CalculateTransfer(
		GridCell<Velocity> source,
		GridCell<Velocity> destination,
		int sourceNeighborCount,
		int destinationNeighborCount
	) {
		double divisor = 2 * Math.Max( sourceNeighborCount, destinationNeighborCount );
		return ( source.Cell - destination.Cell ) * ( _viscosity / divisor );
	}

	public Velocity Combine(
		Velocity left,
		Velocity right
	) {
		return left + right;
	}

	public Velocity Negate(
		Velocity value
	) {
		return -value;
	}

	public Velocity Apply(
		GridCell<Velocity> cell,
		Velocity delta
	) {
		return cell.Cell + delta;
	}

}
