using UnityEngine;
using ActInterfaces;

public static class TargetAnchorUtil2D
{
	public static Vector3 ResolveReachablePoint(
		Vector3 origin,
		Vector3 desired,
		LayerMask wallsMask,
		float radius = 0.0f,
		float skin = 0.05f)
	{
		Vector2 from = origin;
		Vector2 to = desired;
		Vector2 dir = (to - from);
		float dist = dir.magnitude;
		if (dist <= Mathf.Epsilon) return origin;

		dir /= dist;
		skin = Mathf.Max(0f, skin);

		if (radius > 0f)
		{
			
			var hit = Physics2D.CircleCast(from, radius, dir, dist, wallsMask);
			if (hit.collider != null)
			{
				
				float back = Mathf.Min(skin, hit.distance);
				return hit.point - dir * back;
			}
		}
		else
		{
			var hit = Physics2D.Raycast(from, dir, dist, wallsMask);
			if (hit.collider != null)
			{
				float back = Mathf.Min(skin, hit.distance);
				return hit.point - dir * back;
			}
		}
		return desired;
	}

	public static bool IsPointFree(Vector3 pos, float radius, LayerMask wallsMask)
	{
		if (radius > 0f)
			return Physics2D.OverlapCircle(pos, radius, wallsMask) == null;
		return true;
	}
	public static Vector3 CursorWorld2D(Camera cam, Transform owner, float depthFallback)
	{
		var sp = Input.mousePosition;
		float depth = Mathf.Abs(cam.transform.position.z - owner.position.z);
		if (depth < 0.01f) depth = depthFallback;
		var w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, depth));
		w.z = owner.position.z;
		return w;
	}
	public static Vector2 GetMoveDirOrFacing(Transform t)
	{
		var input = t.GetComponent<IMovable>() ?? t.GetComponentInChildren<IMovable>();
		if (input == null) Debug.Log("Have �� Children?");
		Vector2 mv = input != null ? input.LastMoveDir : Vector2.zero;
		if (mv.sqrMagnitude < 0.01f) mv = Vector2.zero;
		return mv.normalized;
	}
	public static bool TryGetMoveDir(Transform t, out Vector2 dir, float eps = 0.01f)
	{
		dir = Vector2.zero;
		var m = t.GetComponent<IMovable>() ?? t.GetComponentInChildren<IMovable>();
		if (m == null) return false;
		var mv = m.LastMoveDir;
		if (mv.sqrMagnitude < eps * eps) return false;
		dir = mv.normalized;
		return true;
	}
	public static Vector3 ResolveReachablePoint2D(Vector3 origin, Vector3 desired, LayerMask walls, float radius, float skin)
	{
		Vector2 from = origin, to = desired, dir = to - from; float dist = dir.magnitude;
		if (dist <= Mathf.Epsilon) return origin;
		dir /= dist; skin = Mathf.Max(0f, skin);

		float eps = 0.03f;
		float minSep = Mathf.Max(skin, radius + eps); // ★ 최소 분리거리 보장

		if (radius > 0f)
		{
			var hit = Physics2D.CircleCast(from, radius, dir, dist, walls);
			if (hit.collider)
			{
				var p = hit.point - dir * Mathf.Min(hit.distance, minSep);
				p += (Vector2)hit.normal * 0.01f; // ★ 법선 바깥 소량 오프셋
				return p;
			}
		}
		else
		{
			var hit = Physics2D.Raycast(from, dir, dist, walls);
			if (hit.collider)
			{
				var p = hit.point - dir * Mathf.Min(hit.distance, minSep);
				p += (Vector2)hit.normal * 0.01f;
				return p;
			}
		}
		return desired;
	}
}
