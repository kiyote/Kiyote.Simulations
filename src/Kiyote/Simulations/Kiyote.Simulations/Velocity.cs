namespace Kiyote.Simulations;

// A simple 2D vector representing flow velocity at a cell. Mirrors
// Kiyote.Simulations.Grids.Velocity but uses float components to match the
// float-based grids used by the Diffusion/Pressure/Projection/Advection simulations.
public readonly struct Velocity : IEquatable<Velocity> {

	public static readonly Velocity Zero = new( 0f, 0f );

	public float X { get; }
	public float Y { get; }

	public float Magnitude => MathF.Sqrt( ( X * X ) + ( Y * Y ) );

	public Velocity(
		float x,
		float y
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
		float scalar
	) {
		return new Velocity( value.X * scalar, value.Y * scalar );
	}

	public static Velocity Divide(
		Velocity value,
		float scalar
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
		float scalar
	) {
		return Multiply( value, scalar );
	}

	public static Velocity operator /(
		Velocity value,
		float scalar
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
