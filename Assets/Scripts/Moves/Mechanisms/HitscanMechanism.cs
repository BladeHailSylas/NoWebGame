using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moves.Mechanisms
{
    [Serializable]
    /// <summary>
    /// Defines how hitscan-based skills behave — instant ray-based hit detection.
    /// </summary>
    public class HitscanMechanism : NewMechanism, INewMechanism
    {
        [Header("Entity Settings")]
        public GameObject hitEffectPrefab;   // Placeholder — not used yet.

        [Header("FollowUp")]
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        [Header("Debug")]
        public bool debugDraw = true;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not HitscanMechanism mech) return;
            if (!ctx.Target.TryGetPosition(out var targetPosition))
            {
                return;
            }

            var origin = ctx.Caster != null ? (Vector2)ctx.Caster.position : Vector2.zero;
            var direction = (targetPosition - origin).normalized;

            var hit = Physics2D.Raycast(origin, direction, mech.MaxRange, mech.Mask);
            if (mech.debugDraw)
            {
                var c = hit ? Color.red : Color.yellow;
                Debug.DrawRay(origin, direction * mech.MaxRange, c, 0.5f);
            }

            if (hit.collider is not null)
            {
                SkillUtils.ActivateChain(mech.onHit, ctx, hit.transform);
            }

            SkillUtils.ActivateChain(mech.onExpire, ctx);
        }
    }
}
