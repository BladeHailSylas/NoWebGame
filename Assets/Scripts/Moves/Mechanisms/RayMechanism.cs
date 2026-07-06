using System.Collections.Generic;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class RayMechanism : NewMechanism, INewMechanism
    {
        [Header("Ray")]
        [SerializeReference] public float rangeMultiplier = 1f;
        [SerializeReference] public List<MechanismData> onHit;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not RayMechanism mech) return;

            Vector2 origin = ctx.Caster.position;
            Vector2 target = ctx.Target.position;
            var dist = target - origin;
            var direction = dist.normalized;
            var distance = dist.magnitude * mech.rangeMultiplier;

            // Detect both hostile targets and blockers in distance order.
            var hits = Physics2D.RaycastAll(origin, direction, distance, LayerMask.GetMask("Foe", "Walls&Obstacles"));
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Walls&Obstacles"))
                    break;

                if (!hit.collider.TryGetComponent<Entity>(out _)) continue;

                if (mech.onHit.Count == 0)
                {
                    if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                    AnchorRegistry.Instance.Return(anchor);
                    continue;
                }

                SkillUtils.ActivateChain(mech.onHit, ctx);
            }

            /* Debug helper: keep the ray visible for a short duration when tuning hit logic. */
            Debug.DrawRay(origin, direction * distance, Color.blue, 0.1f);
        }
    }
}
