// MoveParams.cs (REFACTOR/EXTEND)
using System.Collections.Generic;
using UnityEngine;
using SkillInterfaces;
using UnityEditor;
using UnityEditor.UIElements;

[System.Serializable]
public class MeleeParams : INewParams
{
	[Header("Area")]
	public float radius = 1.6f;
	[Range(0, 360)] public float angleDeg = 120f;
	public LayerMask enemyMask;
	
	[Header("Timing")]
	public byte onAttackDelay;
	public short afterDelay;
	public short cooldownTicks;
	public short CooldownTicks => cooldownTicks;
	
	public List<MechanismRef> onHit = new();
	public List<MechanismRef> onExpire = new();
}

[System.Serializable]
public class MissileParams : ISkillParams, ICooldownParams, IFollowUpProvider, ITargetingData
{
	[Header("Projectile")]
	public float speed = 10f;
	public float acceleration = 0f;
	public float maxTurnDegPerSec = 360f;
	public float radius = 0.2f;
	public float maxRange = 12f;
	public float maxLife = 3f;

	[Header("Collision")]
	public LayerMask enemyMask;
	public LayerMask blockerMask;

	[Header("Damage")]
	public float damage = 8f, apRatio = 0f, knockback = 0f;

	[Header("Timing")]
	public float startDelay = 0.25f;
	public float cooldown = 10f;
	public float endDelay = 0.05f;
	public float Cooldown => cooldown;

        [Header("Behavior")]
        public bool retargetOnLost = true;
        public float retargetRadius = 3f;

        [Header("Targeting")]
        [Tooltip("Toggle to deliberately target the caster instead of searching for another entity.")]
        public bool targetSelf = false;
        public TargetMode mode;
        public float fallback = 10f;
        public Vector2 local;
        public float col;
        public float skin;
        public bool canpen;

        public TargetMode Mode => mode;
        public float FallbackRange => fallback;
        public Vector2 LocalOffset => local;
        public LayerMask WallsMask => blockerMask;
        public LayerMask TargetMask => enemyMask;
        public bool TargetSelf => targetSelf;
        public float CollisionRadius => col;
        public float AnchorSkin => skin;
        public bool CanPenetrate => canpen;


	// ★ FollowUp 예: 맞으면 폭발, 소멸하면 잔류 디버프…
	public List<MechanicRef> onHit = new();
	public List<MechanicRef> onExpire = new();

	public IEnumerable<(CastOrder, float, bool)> BuildFollowUps(AbilityHook hook, Transform prevTarget)
	{
		var source = hook switch
		{
			AbilityHook.OnHit => onHit,
			AbilityHook.OnCastEnd => onExpire,
			_ => null,
		};

		if (source == null) yield break;

		foreach (var (order, delay, respectBusy) in EnumerateFollowUps(source, prevTarget))
		{
			yield return (order, delay, respectBusy);
		}
	}

	static IEnumerable<(CastOrder order, float delay, bool respectBusy)> EnumerateFollowUps(List<MechanicRef> source, Transform prevTarget)
	{
		if (source == null) yield break;
		foreach (var reference in source)
		{
			if (reference.TryBuildOrder(prevTarget, out var order))
			{
				yield return (order, reference.delay, reference.respectBusyCooldown);
			}
		}
	}
}
[System.Serializable]
public class DashParams : ISkillParams, ICooldownParams, IFollowUpProvider, ITargetingData, IAnchorClearance
{
	[Header("Targeting (Runner가 해석)")]
        [SerializeField] TargetMode mode = TargetMode.TowardsMovement;
        [SerializeField] float fallbackRange = 4f;
        [SerializeField] Vector2 localOffset = Vector2.zero;
        [SerializeField] LayerMask wallsMask;
        [SerializeField] bool canpen;
        [SerializeField] bool targetSelf;

        public TargetMode Mode => mode;
        public float FallbackRange => fallbackRange;
        public Vector2 LocalOffset => localOffset;
        public LayerMask WallsMask => wallsMask;
        public LayerMask TargetMask => enemyMask;
        public bool TargetSelf => targetSelf;
        public bool CanPenetrate => canpen;

	[Header("Motion")]
	public float duration = 0.18f;           // 총 대시 시간
	public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
	public bool stopOnWall = true;           // 벽 충돌 시 즉시 종료

	[Header("Collision Volume")]
	public float radius = 0.5f;              // 내 몸의 반경(적/벽 체크 모두에 사용)
	public float skin = 0.05f;               // 충돌면 살짝 넘기는 여유
	public int unitradius = 500;
	public int unitskin = 50;
	// IAnchorClearance
	public float CollisionRadius => radius;
	public float AnchorSkin => Mathf.Max(0.01f, skin);

	[Header("Combat During Dash")]
	public bool dealDamage = true;           // 대시 중 피해를 줄 것인지
	public bool canPenetrate = true;         // 적을 관통할지
	public LayerMask enemyMask;
	public float damage = 8f;
	public float apRatio = 0f;
	public float knockback = 6f;

	[Header("I-Frame/Status")]
	public bool grantIFrame = true;
	public float iFrameDuration = 0.18f;     // 보통 duration과 동일

	[Header("Timing")]
	public float cooldown = 0.35f;
	public float Cooldown => cooldown;

	[Header("FollowUps")]
	public List<MechanicRef> onExpire = new(); // 대시 종료 후 후속(예: 원형 베기)

	public IEnumerable<(CastOrder, float, bool)> BuildFollowUps(AbilityHook hook, Transform prevTarget)
	{
		if (hook != AbilityHook.OnCastEnd) yield break;

		foreach (var (order, delay, respectBusy) in EnumerateFollowUps(onExpire, prevTarget))
		{
			yield return (order, delay, respectBusy);
		}
	}

	static IEnumerable<(CastOrder order, float delay, bool respectBusy)> EnumerateFollowUps(List<MechanicRef> source, Transform prevTarget)
	{
		if (source == null) yield break;
		foreach (var reference in source)
		{
			if (reference.TryBuildOrder(prevTarget, out var order))
			{
				yield return (order, reference.delay, reference.respectBusyCooldown);
			}
		}
	}

}

[System.Serializable]
public class SwitchControllerParams : ISkillParams, ISwitchPolicy
{
	[Tooltip("교대 실행할 기술 목록(타깃/논타깃 혼재 가능)")]
	public List<MechanicRef> steps = new();

	[Min(0)] public int startIndex = 0;
	public bool advanceOnCast = true; // false면 OnHit 등에서 수동 전진

	// 런타임 커서(캐릭터별로 SerializeReference Param이 보관하므로 인스펙터 전역 영향 없음)
	[System.NonSerialized] int _idx = -1;

	public bool TrySelect(Transform owner, Camera cam, Transform prevTarget, out CastOrder order, out MechanicRef reference)
	{
		order = default;
		reference = default;
		if (steps == null || steps.Count == 0) return false;

		if (_idx < 0) _idx = Mathf.Clamp(startIndex, 0, steps.Count - 1);
		var cur = _idx;
		if (advanceOnCast) _idx = (_idx + 1) % steps.Count;

		reference = steps[cur];
		return reference.TryBuildOrder(prevTarget, out order);
	}

	// 필요하면 OnHit에서 수동 전진할 수 있도록 헬퍼 제공
	public void AdvanceExplicit()
	{
		if (steps == null || steps.Count == 0) return;
		_idx = (_idx + 1) % steps.Count;
	}
}