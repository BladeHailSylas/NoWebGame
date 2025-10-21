using SkillInterfaces;
using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Mechanics/Projectile (Homing, Targeted)")]
public class ProjectileMechanism : SkillMechanismBase<MissileParams>, ITargetedMechanic
{
	public override IEnumerator Cast(Transform owner, Camera cam, MissileParams p)
	{
		return Execute(owner, cam, p, null);
	}

	public IEnumerator Cast(Transform owner, Camera cam, ISkillParam param, Transform target)
	{
		if (param is not MissileParams missile)
		{
			Debug.LogError("ProjectileMechanism: 파라미터 타입이 MissileParams가 아닙니다.");
			yield break;
		}

		yield return Execute(owner, cam, missile, target);
	}

	IEnumerator Execute(Transform owner, Camera cam, MissileParams p, Transform explicitTarget)
	{
		if (!owner)
		{
			Debug.LogWarning("ProjectileMechanism: Owner가 없어 발사할 수 없습니다.");
			yield break;
		}

		MechanismRuntimeUtil.QueueFollowUps(p, AbilityHook.OnCastStart, explicitTarget, "Projectile");

		if (p.startDelay > 0f)
		{
			yield return new WaitForSeconds(p.startDelay);
			if (!owner) yield break;
		}

				var solution = TargetingRuntimeUtil.Resolve(owner, cam, p, explicitTarget, createAnchor: true, targetAlly: p.TargetSelf);
		Transform resolvedTarget = solution.Target;
		Debug.Log($"ProjectileMechanism: 발사 대상 {resolvedTarget?.name ?? "null"}, 방향 {solution.Direction}, 거리 {solution.Distance}");
		if (resolvedTarget == null)
		{
			Debug.LogWarning("ProjectileMechanism: 타깃을 찾지 못해 발사를 중지합니다.");
			solution.DisposeAnchor();
			yield break;
		}

		var projectile = new GameObject("HomingProjectile");
		projectile.transform.position = owner.position;
		solution.AdoptAnchor(projectile.transform);

		var mover = projectile.AddComponent<ProjectileMovement>();
		mover.Init(p, owner, resolvedTarget);

		if (p.endDelay > 0f)
		{
			yield return new WaitForSeconds(p.endDelay);
		}

		var followUpTarget = solution.IsSyntheticTarget ? explicitTarget : resolvedTarget;
		MechanismRuntimeUtil.QueueFollowUps(p, AbilityHook.OnCastEnd, followUpTarget, "Projectile");
		/** Projectiles가 OnHit 이벤트를 발행하도록 확장하려면 ProjectileMovement에서 MechanismRuntimeUtil을 호출하십시오. */
	}
}
