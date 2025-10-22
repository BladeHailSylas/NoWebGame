using System;
using UnityEngine;
/// <summary>
/// Represents a deterministic 2D vector where 1.0f equals 1000 fixed units.
/// </summary>
[Serializable]
public readonly struct FixedVector2 : IEquatable<FixedVector2>
{
	public const int UnitsPerFloat = 1024;

	readonly int _rawX;
	readonly int _rawY;

	/// <summary>
	/// Raw X component (in fixed units).
	/// </summary>
	public int RawX => _rawX;

	/// <summary>
	/// Raw Y component (in fixed units).
	/// </summary>
	public int RawY => _rawY;
	/// <summary>
	/// Normalized vector (unit length). Returns (0,0) if the vector is zero.
	/// </summary>
	public FixedVector2 Normalized 
	{ 
		get
		{
			double length = MagnitudeDouble;
			if (length < 1e-6)
			{
				return new FixedVector2(0, 0);
			}
			return new FixedVector2((int)(RawX / length), (int)(RawY / length));
		}
	}
	/// <summary>
	/// The size (magnitude) of the vector in fixed units.
	/// </summary>
	public int Magnitude => (int)Math.Sqrt(RawX * RawX + RawY * RawY);
	public double MagnitudeDouble => Math.Sqrt(RawX * RawX + RawY * RawY);
	public int SqrMagnitude => RawX * RawX + RawY * RawY;

	public FixedVector2(int rawX, int rawY)
	{
		_rawX = rawX;
		_rawY = rawY;
	}

	public FixedVector2(float x, float y)
	{
		_rawX = (int)Math.Round(x * UnitsPerFloat);
		_rawY = (int)Math.Round(y * UnitsPerFloat);
	}

	public FixedVector2(Vector2 vector)
	{
		_rawX = (int)Math.Round(vector.x * UnitsPerFloat);
		_rawY = (int)Math.Round(vector.y * UnitsPerFloat);
	}

	/// <summary>
	/// Converts to the Unity floating-point representation.
	/// </summary>
	public Vector2 AsVector2 => new(_rawX / (float)UnitsPerFloat, _rawY / (float)UnitsPerFloat);
	public Vector2 ToVector2() => AsVector2;

	public static FixedVector2 FromVector2(Vector2 vector)
	{
		return new FixedVector2(vector);
	}

	public static int Dot(FixedVector2 a, FixedVector2 b)
	{
		return a._rawX * b._rawX + a._rawY + b._rawY;
	}
	public static FixedVector2 operator +(FixedVector2 a, FixedVector2 b)
	{
		return new FixedVector2(a._rawX + b._rawX, a._rawY + b._rawY);
	}

	public static FixedVector2 operator -(FixedVector2 a, FixedVector2 b)
	{
		return new FixedVector2(a._rawX - b._rawX, a._rawY - b._rawY);
	}

	public static FixedVector2 operator -(FixedVector2 value)
	{
		return new FixedVector2(-value._rawX, -value._rawY);
	}

	public static FixedVector2 operator *(FixedVector2 vector, int scalar)
	{
		return new FixedVector2(scalar * vector._rawX, scalar * vector._rawY);
	}

	public static FixedVector2 operator *(int scalar, FixedVector2 vector)
	{
		return new FixedVector2(scalar * vector._rawX, scalar * vector._rawY);
	}
	public override string ToString()
	{
		return $"({AsVector2.x:F3}, {AsVector2.y:F3})";
	}

	/// <summary>
	/// Squared distance between two fixed vectors in squared fixed units.
	/// </summary>
	public static int DistanceSquared(FixedVector2 a, FixedVector2 b)
	{
		long dx = (long)a._rawX - b._rawX;
		long dy = (long)a._rawY - b._rawY;
		return (int)Math.Sqrt(dx * dx + dy * dy);
	}

	public bool Equals(FixedVector2 other)
	{
		return _rawX == other._rawX && _rawY == other._rawY;
	}

	public override bool Equals(object obj)
	{
		return obj is FixedVector2 other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(_rawX, _rawY);
	}
}