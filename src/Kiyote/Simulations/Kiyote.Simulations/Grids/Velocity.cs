namespace Kiyote.Simulations.Grids;

// A simple 2D vector representing airflow/wind velocity at a cell.
public readonly struct Velocity : IEquatable<Velocity> {

	public static readonly Velocity Zero = new( 0.0, 0.0 );

	public double X { get; }
	public double Y { get; }

	public Velocity(
		double x,
		double y
	) {
		X = x;
		Y = y;
	}

	public static Velocity Add(
		Velocity left,
		Velocity right
	) {
		return new Velocity( left.X + right.X, left.Y + right.Y );
	}

	public static Velocity Subtract(
		Velocity left,
		Velocity right
	) {
		return new Velocity( left.X - right.X, left.Y - right.Y );
	}

	public static Velocity Negate(
		Velocity value
	) {
		return new Velocity( -value.X, -value.Y );
	}

	public static Velocity Multiply(
		Velocity value,
		double scalar
	) {
		return new Velocity( value.X * scalar, value.Y * scalar );
	}

	public static Velocity Divide(
		Velocity value,
		double scalar
	) {
		return new Velocity( value.X / scalar, value.Y / scalar );
	}

	public static Velocity operator +(
		Velocity left,
		Velocity right
	) {
		return Add( left, right );
	}

	public static Velocity operator -(
		Velocity left,
		Velocity right
	) {
		return Subtract( left, right );
	}

	public static Velocity operator -(
		Velocity value
	) {
		return Negate( value );
	}

	public static Velocity operator *(
		Velocity value,
		double scalar
	) {
		return Multiply( value, scalar );
	}

	public static Velocity operator /(
		Velocity value,
		double scalar
	) {
		return Divide( value, scalar );
	}

	public static bool operator ==(
		Velocity left,
		Velocity right
	) {
		return left.Equals( right );
	}

	public static bool operator !=(
		Velocity left,
		Velocity right
	) {
		return !left.Equals( right );
	}

	public bool Equals(
		Velocity other
	) {
		return X.Equals( other.X ) && Y.Equals( other.Y );
	}

	public override bool Equals(
		object? obj
	) {
		return obj is Velocity other && Equals( other );
	}

	public override int GetHashCode() {
		return HashCode.Combine( X, Y );
	}

}
