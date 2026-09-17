using System.Diagnostics.CodeAnalysis;
using Kiyote.Simulations.Grids;

namespace Kiyote.Simulations.Visualizer.GridFluid;

// The visualizer's grid cell type is Velocity itself, so this accessor is just an identity mapping.
[ExcludeFromCodeCoverage]
internal readonly struct IdentityVelocityAccessor : IVelocityAccessor<Velocity> {

	public Velocity GetVelocity(
		Velocity cell
	) {
		return cell;
	}

	public Velocity WithVelocity(
		Velocity cell,
		Velocity velocity
	) {
		return velocity;
	}

}
