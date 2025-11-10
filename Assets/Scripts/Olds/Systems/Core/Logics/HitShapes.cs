using System;
using UnityEngine;
#region ===== Hit & Collision =====
public interface IHitShape
{
	bool Overlaps(IHitShape shape);
	IHitShape[] OverlapShapes();
}
/// <summary>
/// Axis-aligned box expressed in fixed-space coordinates.
/// </summary>
[Serializable]
public struct HitBox : IHitShape
{
	public FixedVector2 center;
	public FixedVector2 halfSize;

	public HitBox(FixedVector2 center, FixedVector2 halfSize)
	{
		this.center = center;
		this.halfSize = halfSize;
	}

	public readonly int MinX => center.RawX - halfSize.RawX;
	public readonly int MaxX => center.RawX + halfSize.RawX;
	public readonly int MinY => center.RawY - halfSize.RawY;
	public readonly int MaxY => center.RawY + halfSize.RawY;

	public readonly bool Overlaps(IHitShape shape)
	{
		return shape switch
		{
			HitBox box => Overlaps(box),
			HitCircle circle => Overlaps(circle),
			_ => throw new NotSupportedException($"Unsupported shape type: {shape.GetType().Name}"),
		};
	}
	public readonly bool Overlaps(HitBox box)
	{
		var dx = Math.Abs((long)center.RawX - box.center.RawX);
		var dy = Math.Abs((long)center.RawY - box.center.RawY);
		var limitX = (long)halfSize.RawX + box.halfSize.RawX;
		var limitY = (long)halfSize.RawY + box.halfSize.RawY;
		var separated = dx > limitX || dy > limitY;
		return !separated;
	}

	public readonly bool Overlaps(HitCircle circle)
	{
		var clampedX = Mathf.Clamp(circle.center.RawX, MinX, MaxX);
		var clampedY = Mathf.Clamp(circle.center.RawY, MinY, MaxY);

		long dx = circle.center.RawX - clampedX;
		long dy = circle.center.RawY - clampedY;
		long radius = circle.radius;
		return (dx * dx + dy * dy) <= radius * radius;
	}
	public readonly IHitShape[] OverlapShapes()
	{
		return new IHitShape[] { this };
	}
}

/// <summary>
/// Circle expressed in fixed-space coordinates.
/// </summary>
[Serializable]
public struct HitCircle : IHitShape
{
	public FixedVector2 center;
	public int radius; // in fixed units

	public HitCircle(FixedVector2 center, int radius)
	{
		this.center = center;
		this.radius = Math.Max(0, radius);
	}
	public readonly bool Overlaps(IHitShape shape)
	{
		return shape switch
		{
			HitBox box => Overlaps(box),
			HitCircle circle => Overlaps(circle),
			_ => throw new NotSupportedException($"Unsupported shape type: {shape.GetType().Name}"),
		};
	}
	public readonly bool Overlaps(HitCircle circle)
	{
		var radii = (long)radius + circle.radius;
		return FixedVector2.DistanceSquared(center, circle.center) <= radii * radii;
	}

	public readonly bool Overlaps(HitBox box)
	{
		return box.Overlaps(this);
	}
	public readonly IHitShape[] OverlapShapes()
	{
		return new IHitShape[] { this };
	}

}
public sealed class FixedCollision
{
	public static bool CheckOverlap(IHitShape shape1, IHitShape shape2)
	{
		return shape1 switch
		{
			HitCircle circle1 when shape2 is HitCircle circle2 => circle1.Overlaps(circle2),
			HitCircle circle when shape2 is HitBox box => box.Overlaps(circle),
			HitBox box when shape2 is HitCircle circle => box.Overlaps(circle),
			HitBox box1 when shape2 is HitBox box2 => box1.Overlaps(box2),
			_ => false,
		};
	}
	/// <summary>
	/// 두 충돌체 사이의 법선(normal)과 침투 깊이(depth)를 계산합니다.
	/// 겹치지 않은 경우 null을 반환합니다.
	/// </summary>
	public static ContactInfo? ComputeContact(IHitShape shape1, IHitShape shape2)
	{
		switch (shape1)
		{
			case HitCircle a when shape2 is HitCircle b:
				return ComputeCircleCircle(a, b);
			case HitCircle a when shape2 is HitBox b:
				return ComputeCircleBox(a, b);
			default:
				return null;
		}
	}

	private static ContactInfo? ComputeCircleCircle(HitCircle a, HitCircle b)
	{
		var diff = b.center - a.center;
		var distSq = (long)diff.RawX * diff.RawX + (long)diff.RawY * diff.RawY;
		var radii = (long)a.radius + b.radius;
		var radiiSq = radii * radii;
		if (distSq >= radiiSq)
			return null;

		var dist = Math.Sqrt(distSq);
		var depth = radii - dist;
		var normal = (dist > 1e-6)
			? new FixedVector2((int)(diff.RawX / dist), (int)(diff.RawY / dist))
			: new FixedVector2(0, 0);

		return new ContactInfo
		{
			normal = normal,
			depth = (int)Math.Round(depth),
			owner = b
		};
	}

	private static ContactInfo? ComputeCircleBox(HitCircle circle, HitBox box)
	{
		var clampedX = Math.Max(box.MinX, Math.Min(circle.center.RawX, box.MaxX));
		var clampedY = Math.Max(box.MinY, Math.Min(circle.center.RawY, box.MaxY));
		var closest = new FixedVector2(clampedX, clampedY);
		var diff = circle.center - closest;

		var distSq = (long)diff.RawX * diff.RawX + (long)diff.RawY * diff.RawY;
		if (distSq > (long)circle.radius * circle.radius)
			return null;

		var dist = Math.Sqrt(distSq);
		var depth = circle.radius - dist;
		var normal = (dist > 1e-6)
			? new FixedVector2((int)(diff.RawX / dist), (int)(diff.RawY / dist))
			: new FixedVector2(0, 0);

		return new ContactInfo
		{
			normal = normal,
			depth = (int)Math.Round(depth),
			owner = box
		};
	}
}
public struct ContactInfo
{
	public FixedVector2 normal; // 침투 방향 (정규화)
	public int depth;           // 침투 깊이 (fixed 단위)
	public object owner;        // 충돌체 소유자 (선택적)
}
#endregion