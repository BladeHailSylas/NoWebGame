using System.Collections.Generic;
using Moves;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(menuName = "Skills/Mechanisms/Melee")]
    public class MeleeMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not MeleeParams param)
                return;

            var caster = ctx.Caster;
            var target = ctx.Target ?? caster; // anchor fallback

            Vector2 origin = caster.position;
            Vector2 dir = target.position - caster.position;
            var radius = param.radius;
            var halfAngle = param.angleDeg * 0.5f;

            // 1) 원형 탐색
            var hits = Physics2D.OverlapCircleAll(origin, radius, param.enemyMask);

            // this stores evaluated targets
            var hitSomething = false;

            foreach (var hit in hits)
            {
                // 자기 자신 제거
                if (hit.transform == caster)
                    continue;

                var toTarget = ((Vector2)hit.transform.position - origin).normalized;

                // angle filter (부채꼴)
                if (param.angleDeg < 360f)
                {
                    var angle = Vector2.Angle(dir, toTarget);
                    if (angle > halfAngle)
                        continue;
                }

                hitSomething = true;
            
                // OnHit follow-ups 실행
                foreach (var followup in param.onHit)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;
                    SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                        mech, followup.@params, ctx.Damage, hit.transform);

                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
            Debug.Log(hitSomething);
            // 타격 대상이 하나도 없었을 때 onExpire 실행
            if (param.onExpire == null) return;
            {
                foreach (var followup in param.onExpire)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;
                    SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                        mech, followup.@params, ctx.Damage, ctx.Target);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
        }
    }
}

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
