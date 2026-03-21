using System.Collections.Generic;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    /// <summary>
    /// Defines how hitscan-based skills behave — instant ray-based hit detection.
    /// </summary>
    [CreateAssetMenu(fileName = "HitscanMechanism", menuName = "Skills/Mechanisms/Hitscan")]
    public class HitscanMechanism : NewMechanism
    {
        [Header("Entity Settings")]
        public GameObject hitEffectPrefab;   // Placeholder — not used yet.

        [Header("FollowUp")]
        public List<SkillData> onHit;
        public List<SkillData> onExpire;

        [Header("Debug")]
        public bool debugDraw = true;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not HitscanMechanism mech) return;
            if (ctx.Target is null)
            {
                return;
            }

            if ((mech.Mask.value & (1 << ctx.Target.gameObject.layer)) == 0)
            {
                Debug.Log("[HitscanMechanism] Target layer not allowed — skipping.");
                return;
            }

            Vector2 origin = ctx.Caster != null ? (Vector2)ctx.Caster.position : Vector2.zero;
            var direction = ((Vector2)ctx.Target.position - origin).normalized;
            var distance = Vector2.Distance(origin, ctx.Target.position);

            if (distance > mech.MaxRange || distance < mech.MinRange)
            {
                Debug.Log($"[HitscanMechanism] Target out of range ({distance:F2}) — skipping.");
                return;
            }

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
