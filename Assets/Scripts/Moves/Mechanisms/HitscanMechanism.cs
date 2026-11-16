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

        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not HitscanMechanism mech) return;
            if (!ctx.TryGetSpatialPosition(out var targetPosition))
            {
                return;
            }

            if (mech.Targeting.IsRelative)
            {
                RelativeHitQueries.Ray(ctx, mech.MaxRange, true, mech.onHit);
                SkillUtils.ActivateChain(mech.onExpire, ctx, trigger: "onExpire");
                return;
            }
            var origin = ctx.Caster != null ? (Vector2)ctx.Caster.position : Vector2.zero;
            var direction = (targetPosition - origin).normalized;

            var hit = Physics2D.Raycast(origin, direction, mech.MaxRange, mech.Mask);
            AttackLog.Write(ctx.Trace, "HitscanQuery", $"origin={origin} direction={direction} distance={mech.MaxRange} queryLayers={mech.Mask.value}");
            AttackLog.Hit(ctx, hit.collider, hit ? "Accepted" : "NoHit");
            if (mech.debugDraw)
            {
                var c = hit ? Color.red : Color.yellow;
                Debug.DrawRay(origin, direction * mech.MaxRange, c, 0.5f);
            }

            if (hit.collider is not null)
            {
                SkillUtils.ActivateChain(mech.onHit, ctx, hit.transform, trigger: "onHit");
            }

            SkillUtils.ActivateChain(mech.onExpire, ctx, trigger: "onExpire");
        }
    }
}
