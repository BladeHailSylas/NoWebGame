using System.Collections.Generic;
using Moves.Effects.Definitions;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class MeleeMechanism : NewMechanism
    {
        [Header("Area")]
        [Range(0, 360)] public float angleDeg = 120f;
        public LayerMask enemyMask;
        public MeleeEffectEntity effectPrefab;

        [Header("Callbacks")]
        public List<MechanismData> onHit = new();
        public List<MechanismData> onExpire = new();

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not MeleeMechanism mech)
                return;

            var caster = ctx.Caster;
            var target = ctx.Target; // Anchor fallback remains unchanged.

            Vector2 origin = caster.position;
            Vector2 dir = target.position - caster.position;
            var radius = mech.MaxRange;
            var halfAngle = mech.angleDeg * 0.5f;
            mech.effectPrefab?.Init(origin, radius, mech.angleDeg, dir);

            var hits = Physics2D.OverlapCircleAll(origin, radius, mech.enemyMask);
            foreach (var hit in hits)
            {
                if (hit.transform == caster)
                    continue;

                var toTarget = ((Vector2)hit.transform.position - origin).normalized;
                if (mech.angleDeg < 360f)
                {
                    var angle = Vector2.Angle(dir, toTarget);
                    if (angle > halfAngle)
                        continue;
                }

                SkillUtils.ActivateFollowUp(mech.onHit, ctx, hit.transform);
            }

            if (mech.onExpire.Count == 0)
            {
                if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                AnchorRegistry.Instance.Return(anchor);
                return;
            }

            foreach (var followup in mech.onExpire)
            {
                if (followup.mechanism is not INewMechanism mechFollowUp) continue;
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mechFollowUp, ctx.Damage, ctxTarget, ctx.Var, ctx.Mech.Mask);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }
    }
}
