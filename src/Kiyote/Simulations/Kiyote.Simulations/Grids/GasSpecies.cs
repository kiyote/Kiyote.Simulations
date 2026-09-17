using Kiyote.Geometry.Grids;

namespace Kiyote.Simulations.Grids;

// Bundles everything needed to advect one gas species' concentration field.
public readonly struct GasSpecies<TScalar, TScalarSetCell> : IEquatable<GasSpecies<TScalar, TScalarSetCell>>
	where TScalarSetCell : ICallbackStrategy<TScalar, TScalar> {

	public IGrid<TScalar> Concentration { get; }
	public TScalarSetCell SetCell { get; }

	public GasSpecies(
		IGrid<TScalar> concentration,
		TScalarSetCell setCell
	) {
		Concentration = concentration;
		SetCell = setCell;
	}

	public static bool operator ==(
		GasSpecies<TScalar, TScalarSetCell> left,
		GasSpecies<TScalar, TScalarSetCell> right
	) {
		return left.Equals( right );
	}

	public static bool operator !=(
		GasSpecies<TScalar, TScalarSetCell> left,
		GasSpecies<TScalar, TScalarSetCell> right
	) {
		return !left.Equals( right );
	}

	public bool Equals(
		GasSpecies<TScalar, TScalarSetCell> other
	) {
		return Equals( Concentration, other.Concentration ) && EqualityComparer<TScalarSetCell>.Default.Equals( SetCell, other.SetCell );
	}

	public override bool Equals(
		object? obj
	) {
		return obj is GasSpecies<TScalar, TScalarSetCell> other && Equals( other );
	}

	public override int GetHashCode() {
		return HashCode.Combine( Concentration, SetCell );
	}

}
