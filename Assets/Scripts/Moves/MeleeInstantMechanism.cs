using SkillInterfaces;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Mechanics/Melee Instant")]
public class MeleeInstantMechanism : SkillMechanismBase<MeleeParams>
{
    public override IEnumerator Cast(Transform owner, Camera cam, MeleeParams p)
    {
        if (!owner)
        {
            Debug.LogWarning("MeleeInstantMechanism: 유효한 Owner가 없어 실행을 중단합니다.");
            yield break;
        }

        MechanismRuntimeUtil.QueueFollowUps(p, AbilityHook.OnCastStart, null, "Melee");
        /** 필요 시 OnCastStart 시점에서 상태 UI를 갱신하려면 위 줄을 확장해 전달하십시오. */

        if (p.windup > 0f)
        {
            yield return new WaitForSeconds(p.windup);
        }

        Vector2 origin = owner.position;
        Vector2 fwd = GetMouseDir(cam, origin);
        float half = p.angleDeg * 0.5f;

        var hits = Physics2D.OverlapCircleAll(origin, p.unitradius, p.enemyMask);
        Transform firstHit = null;
        foreach (var c in hits)
        {
            if (p.angleDeg < 359f)
            {
                Vector2 to = (Vector2)c.bounds.ClosestPoint(origin) - origin;
                if (to.sqrMagnitude > 1e-6f)
                {
                    float ang = Vector2.SignedAngle(fwd, to.normalized);
                    if (Mathf.Abs(ang) > half) continue;
                }
            }

            if (c.TryGetComponent(out ActInterfaces.IVulnerable v))
            {
                v.TakeDamage(p.attack * p.attackPercent, p.apRatio);
                firstHit ??= c.transform;
            }

            if (c.attachedRigidbody)
            {
                var dir = ((Vector2)c.bounds.center - origin).normalized;
                c.attachedRigidbody.AddForce(dir * p.knockback, ForceMode2D.Impulse);
            }
        }

        if (firstHit)
        {
            MechanismRuntimeUtil.QueueFollowUps(p, AbilityHook.OnHit, firstHit, "Melee");
        }

        if (p.recover > 0f)
        {
            yield return new WaitForSeconds(p.recover);
        }

        MechanismRuntimeUtil.QueueFollowUps(p, AbilityHook.OnCastEnd, firstHit, "Melee");
    }

    static Vector2 GetMouseDir(Camera cam, Vector2 from)
    {
        if (!cam) return Vector2.right;
        var m = cam.ScreenToWorldPoint(Input.mousePosition);
        m.z = 0f; return ((Vector2)m - from).normalized;
    }
}
