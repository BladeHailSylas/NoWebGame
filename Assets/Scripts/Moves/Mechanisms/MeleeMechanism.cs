using System.Collections.Generic;
using Moves;
using PlayerScripts.Skills;
using Systems.Anchor;
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
            var target = ctx.Target; // anchor fallback

            Vector2 origin = caster.position;
            Vector2 dir = target.position - caster.position;
            var radius = param.MaxRange;
            var halfAngle = param.angleDeg * 0.5f;

            // 1) 원형 탐색
            var hits = Physics2D.OverlapCircleAll(origin, radius, param.enemyMask);

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
                //Debug.Log(hit.transform.name);
                // OnHit follow-ups 실행
                SkillUtils.ActivateFollowUp(param.onHit, ctx, hit.transform);
            }
            {
                if (param.onExpire.Count == 0)
                {
                    if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                    AnchorRegistry.Instance.Return(anchor);
                }
                foreach (var followup in param.onExpire)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;
                    var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                    SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                        mech, followup.@params, ctx.Damage, ctxTarget);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
        }
    }
}

[System.Serializable]
public class MeleeParams : NewParams
{
    [Header("Area")]
    [Range(0, 360)] public float angleDeg = 120f;
    public LayerMask enemyMask;
    public List<MechanismRef> onHit = new();
    public List<MechanismRef> onExpire = new();
}
