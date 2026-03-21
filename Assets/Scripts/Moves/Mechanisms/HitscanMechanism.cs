using System.Collections.Generic;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    /// <summary>
    /// Defines how hitscan-based skills behave — instant ray-based hit detection.
    /// </summary>
    public class HitscanMechanism : NewMechanism
    {
        [Header("Entity Settings")]
        public GameObject hitEffectPrefab;   // Placeholder — not used yet.

        [Header("FollowUp")]
        public List<MechanismData> onHit;
        public List<MechanismData> onExpire;

        [Header("Debug")]
        public bool debugDraw = true;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not HitscanMechanism mech) return;
            if (ctx.Target is null)
            {
                return;
            }

            var origin = ctx.Caster != null ? (Vector2)ctx.Caster.position : Vector2.zero;
            var direction = ((Vector2)ctx.Target.position - origin).normalized;

            var hit = Physics2D.Raycast(origin, direction, mech.MaxRange, mech.Mask);
            if (mech.debugDraw)
            {
                var c = hit ? Color.red : Color.yellow;
                Debug.DrawRay(origin, direction * mech.MaxRange, c, 0.5f);
            }

            if (hit.collider is not null)
            {
                SkillUtils.ActivateFollowUp(mech.onHit, ctx, hit.transform);
            }

            SkillUtils.ActivateFollowUp(mech.onExpire, ctx);
        }
    }
}
