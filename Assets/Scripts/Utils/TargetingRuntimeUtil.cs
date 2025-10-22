using ActInterfaces;
using SkillInterfaces;
using UnityEngine;

[System.Obsolete("This will be removed in future versions")]
/// <summary>
///     타깃팅과 관련된 런타임 계산을 표준화합니다. SkillRunner/IntentOrchestrator 체계를 고려해 재사용 가능한 유틸리티를 제공합니다.
/// </summary>
public static class TargetingRuntimeUtil {
	/// <summary>
	///     Synthetic Anchor에 부여하는 이름입니다. ProjectileMovement 등 기존 코드와 호환하기 위해 상수로 정의합니다.
	/// </summary>
	public const string AnchorName = "Anchor";

	const float MinDirectionSqr = 1e-4f;

	/// <summary>
	///     현재 캐스트 환경에서 사용할 타깃 정보를 계산합니다.
	/// </summary>
	/// <param name="owner">기술을 시전하는 주체입니다.</param>
	/// <param name="cam">시야 계산에 활용할 카메라입니다.</param>
	/// <param name="data">타깃팅 모드를 정의한 파라미터입니다.</param>
	/// <param name="explicitTarget">Intent에서 이미 결정된 대상입니다.</param>
	/// <param name="createAnchor">명시적 대상이 없을 때 Anchor GameObject를 생성할지 여부입니다.</param>
	/// <param name="targetAlly">아군(자신 포함)을 타깃으로 삼을 수 있는지 결정합니다.</param>
	public static TargetingResult Resolve(Transform owner, Camera cam, ITargetingData data, Transform explicitTarget, bool createAnchor = false, bool targetAlly = false)
	{
		if (owner == null)
		{
			return new TargetingResult(explicitTarget, Vector2.right, 0f, Vector2.zero, null, false);
		}

		Vector2 origin = owner.position;

		if (explicitTarget != null)
		{
			var offset = (Vector2)explicitTarget.position - origin;
			if (offset.sqrMagnitude <= MinDirectionSqr)
			{
				offset = owner.right;
			}
			float distance = offset.magnitude;
			var dir = offset.sqrMagnitude > MinDirectionSqr ? offset.normalized : (Vector2)owner.right;
			return new TargetingResult(explicitTarget, dir, distance, (Vector2)explicitTarget.position, null, false);
		}

		if (targetAlly && owner != null)
		{
			var dirSelf = (Vector2)owner.right;
			if (dirSelf.sqrMagnitude <= MinDirectionSqr)
			{
				dirSelf = Vector2.right;
			}
			return new TargetingResult(owner, dirSelf.normalized, 0f, owner.position, null, false);
		}

		Vector2 desiredPoint = origin;
		Vector2 fallbackDir = owner.right;
		float fallbackDistance = data != null ? Mathf.Max(0f, data.FallbackRange) : 0f;
		bool needsAnchor = createAnchor;

		if (data != null)
		{
			switch (data.Mode)
			{
				case TargetMode.TowardsEntity:
					if (TryResolveEntityTarget(owner, cam, data, out var entityTarget))
					{
						var offset = (Vector2)entityTarget.position - origin;
						var ownerRight = (Vector2)owner.right;
						if (offset.sqrMagnitude <= MinDirectionSqr)
						{
							offset = ownerRight.sqrMagnitude > MinDirectionSqr ? ownerRight : Vector2.right;
						}
						float distance = offset.magnitude;
						var dir = offset.sqrMagnitude > MinDirectionSqr ? offset.normalized : ownerRight;
						if (dir.sqrMagnitude <= MinDirectionSqr)
						{
							dir = Vector2.right;
						}
						return new TargetingResult(entityTarget, dir.normalized, distance, (Vector2)entityTarget.position, null, false);
					}
					desiredPoint = origin + fallbackDir.normalized * Mathf.Max(data.FallbackRange, fallbackDistance);
					fallbackDistance = Mathf.Max(data.FallbackRange, fallbackDistance);
					break;
				case TargetMode.TowardsCursor when cam != null:
					var mouse = cam.ScreenToWorldPoint(Input.mousePosition);
					mouse.z = owner.position.z;
					desiredPoint = mouse;
					fallbackDir = desiredPoint - origin;
					fallbackDistance = fallbackDir.magnitude;
					needsAnchor = true;
					break;
				case TargetMode.TowardsCoordinate:
					desiredPoint = owner.TransformPoint(data.LocalOffset);
					fallbackDir = desiredPoint - origin;
					fallbackDistance = fallbackDir.magnitude;
					needsAnchor = true;
					break;
				case TargetMode.TowardsMovement:
					var mover = owner.GetComponentInChildren<IMovable>(); //owner가 Player가 아니고 SkillRunner임, IMovable은 Player에 붙어있으므로 참조가 불가 => 부모의 자식으로부터 참조하는 건 비효율적인데
																		//owner가 제대로 Player를 가져오게 만드는 방법?
																		//Monobehaviour가 아니라서 GetComponentInChildren 같은 건 불가능, 그렇다면 전역적으로 전달해야 한다?
					if (mover != null && mover.LastMoveDir.sqrMagnitude > MinDirectionSqr)
					{
						fallbackDir = mover.LastMoveDir; //직접 참조하려는 과정에서 문제가 발생하는 것 같음
					}
					else if (owner.TryGetComponent<Rigidbody2D>(out var rb) && rb.linearVelocity.sqrMagnitude > MinDirectionSqr)
					{
						fallbackDir = rb.linearVelocity;
					}
					desiredPoint = origin + fallbackDir.normalized * Mathf.Max(data.FallbackRange, fallbackDistance);
					fallbackDistance = data.FallbackRange;
					needsAnchor = true;
					break;
				default:
					desiredPoint = origin + fallbackDir.normalized * Mathf.Max(data.FallbackRange, fallbackDistance);
					fallbackDistance = Mathf.Max(data.FallbackRange, fallbackDistance);
					break;
			}
		}
		else
		{
			desiredPoint = origin + fallbackDir.normalized * Mathf.Max(fallbackDistance, 0f);
		}

		if (fallbackDir.sqrMagnitude <= MinDirectionSqr)
		{
			fallbackDir = owner.right;
		}
		if (fallbackDir.sqrMagnitude <= MinDirectionSqr)
		{
			fallbackDir = Vector2.right;
		}

		fallbackDir = fallbackDir.normalized;
		if (fallbackDistance <= 0f)
		{
			fallbackDistance = (desiredPoint - origin).magnitude;
			if (fallbackDistance <= 0f)
			{
				fallbackDistance = 0.01f;
			}
		}

		Transform anchor = null;
		bool synthetic = false;
		if (needsAnchor)
		{
			var anchorObj = new GameObject(AnchorName);
			anchorObj.transform.position = desiredPoint;
			anchorObj.hideFlags = HideFlags.DontSave;
			anchor = anchorObj.transform;
			synthetic = true;
		}

		return new TargetingResult(anchor, fallbackDir, fallbackDistance, desiredPoint, anchor, synthetic);
	}

	static bool TryResolveEntityTarget(Transform owner, Camera cam, ITargetingData data, out Transform target)
	{
		target = null;
		if (owner == null)
		{
			return false;
		}

		// 1) 우선 CursorHoverTargetProvider가 존재하면 그것을 그대로 사용합니다.
		var provider = owner.GetComponentInChildren<CursorHoverTargetProvider>();
		if (provider == null)
		{
			Debug.LogError("No CursorHoverTargetProvider found on owner or its children. Did you missed something?");
		}
		if (provider != null && provider.TryGetTarget(out var hovered))
		{
			target = hovered;
			return target != null;
		}

		// 2) Provider가 없다면 CursorHoverTargeter의 로직을 인라인으로 재현합니다.
		if (cam == null || data == null || data.TargetMask.value == 0)
		{
			return false;
		}

		var mouse = cam.ScreenToWorldPoint(Input.mousePosition);
		if (owner != null)
		{
			mouse.z = owner.position.z;
		}
		Vector2 cursor = mouse;
		Vector2 origin = owner ? (Vector2)owner.position : cursor;

		var hits = Physics2D.OverlapPointAll(cursor, data.TargetMask);
		if (hits.Length == 0)
		{
			hits = Physics2D.OverlapCircleAll(cursor, 0.0625f, data.TargetMask);
		}
		if (hits.Length == 0)
		{
			return false;
		}

		float maxRange = data.FallbackRange > 0f ? data.FallbackRange : float.PositiveInfinity;
		Transform best = null;
		float bestDist = float.PositiveInfinity;

		foreach (var h in hits)
		{
			var collider = h;
			var candidate = collider.transform;
			var closest = collider.bounds.ClosestPoint(origin);
			float distance = Vector2.Distance(origin, closest);
			if (distance > maxRange)
			{
				continue;
			}

			bool blocked = false;
			if (data.WallsMask.value != 0)
			{
				var hit = Physics2D.Linecast(origin, closest, data.WallsMask);
				blocked = hit.collider != null && hit.collider.transform != candidate;
			}

			if (blocked)
			{
				continue;
			}

			if (distance < bestDist)
			{
				bestDist = distance;
				best = candidate;
			}
		}

		if (best == null)
		{
			return false;
		}

		target = best;
		return true;
	}

	/// <summary>
	///     Resolve 결과를 담는 구조체입니다. Anchor 관리 기능을 함께 제공합니다.
	/// </summary>
	public readonly struct TargetingResult
	{
		public Transform Target { get; }
		public Vector2 Direction { get; }
		public float Distance { get; }
		public Vector2 TargetPoint { get; }
		public bool IsSyntheticTarget { get; }

		readonly Transform _anchor;

		internal TargetingResult(Transform target, Vector2 direction, float distance, Vector2 targetPoint, Transform anchor, bool synthetic)
		{
			Target = target;
			Direction = direction;
			Distance = distance;
			TargetPoint = targetPoint;
			_anchor = anchor;
			IsSyntheticTarget = synthetic;
		}

		/// <summary>
		///     Anchor를 지정된 부모 아래로 이동시켜 수명 관리를 쉽게 합니다.
		/// </summary>
		public void AdoptAnchor(Transform parent)
		{
			if (_anchor != null)
			{
				_anchor.SetParent(parent, true);
			}
		}

		/// <summary>
		///     Anchor가 더 이상 필요 없을 때 명시적으로 제거합니다.
		/// </summary>
		public void DisposeAnchor()
		{
			if (_anchor != null)
			{
				Object.Destroy(_anchor.gameObject);
			}
		}
	}
}
