namespace Kiyote.Simulations.LowFidelity.Atmospherics;

public struct Vector : IEquatable<Vector> {
	public float X { get; set; }
	public float Y { get; set; }

	public Vector() {
		X = 0.0f;
		Y = 0.0f;
	}

	public Vector(
		float x,
		float y
	) {
		X = x;
		Y = y;
	}

	public static Vector Add(
		Vector left,
		Vector right
	) {
		return new Vector( left.X + right.X, left.Y + right.Y );
	}

	public static Vector Subtract(
		Vector left,
		Vector right
	) {
		return new Vector( left.X - right.X, left.Y - right.Y );
	}

	public static Vector Negate(
		Vector value
	) {
		return new Vector( -value.X, -value.Y );
	}

	public static Vector Multiply(
		Vector value,
		float scalar
	) {
		return new Vector( value.X * scalar, value.Y * scalar );
	}

	public static Vector Divide(
		Vector value,
		float scalar
	) {
		return new Vector( value.X / scalar, value.Y / scalar );
	}
	public static Vector operator +(
		Vector left,
		Vector right
	) {
		return Add( left, right );
	}

	public static Vector operator -(
		Vector left,
		Vector right
	) {
		return Subtract( left, right );
	}

	public static Vector operator -(
		Vector value
	) {
		return Negate( value );
	}

	public static Vector operator *(
		Vector value,
		float scalar
	) {
		return Multiply( value, scalar );
	}

	public static Vector operator /(
		Vector value,
		float scalar
	) {
		return Divide( value, scalar );
	}

	public static bool operator ==(
		Vector left,
		Vector right
	) {
		return left.Equals( right );
	}

	public static bool operator !=(
		Vector left,
		Vector right
	) {
		return !left.Equals( right );
	}

	public readonly bool Equals(
		Vector other
	) {
		return X.Equals( other.X ) && Y.Equals( other.Y );
	}

	public override bool Equals(
		object? obj
	) {
		return obj is Vector other && Equals( other );
	}

	public override readonly int GetHashCode() {
		return HashCode.Combine( X, Y );
	}

}
