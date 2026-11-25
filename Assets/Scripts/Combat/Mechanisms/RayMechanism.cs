using global::NoWebGame.Entities.Core;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using System.Collections.Generic;

using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "RayMechanism")]
    [System.Serializable]
    public class RayMechanism : NewMechanism, INewMechanism
    {
        [Header("Ray")]
        [SerializeReference] public float rangeMultiplier = 1f;
        [SerializeReference] public List<MechanismData> onHit;

        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not RayMechanism mech) return;

            Vector2 origin = ctx.Caster.position;
            if (!ctx.TryGetSpatialPosition(out var target)) return;
            var dist = target - origin;
            var direction = dist.normalized;
            var distance = dist.magnitude * mech.rangeMultiplier;

            if (mech.Targeting.IsRelative) { RelativeHitQueries.Ray(ctx, distance, false, mech.onHit); return; }
            // Detect both hostile targets and blockers in distance order.
            var hits = Physics2D.RaycastAll(origin, direction, distance, LayerMask.GetMask("Foe", "Walls&Obstacles"));
            AttackLog.Write(ctx.Trace, "RayQuery", $"origin={origin} direction={direction} distance={distance} queryLayers={LayerMask.GetMask("Foe", "Walls&Obstacles")} blockerLayers={LayerMask.GetMask("Walls&Obstacles")} colliders={hits.Length}");
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            // 여러 자식 Collider에 맞아도 같은 Entity의 Chain은 한 번만 실행합니다.
            var hitEntities = new HashSet<Entity>();

            foreach (var hit in hits)
            {
                var layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Walls&Obstacles"))
                { AttackLog.Hit(ctx, hit.collider, "WallBlocked"); break; }

                var entity = EntityIdentity.FromCollider(hit.collider);
                if (!EntityIdentity.IsValid(entity)) { AttackLog.Hit(ctx, hit.collider, "RejectedInvalidEntity"); continue; }
                if (!hitEntities.Add(entity)) { AttackLog.Hit(ctx, hit.collider, "SkippedDuplicateEntity"); continue; }

                AttackLog.Hit(ctx, hit.collider, "Accepted");
                // 재탐색 및 ExitPosition 우선순위는 공용 Chain 처리에 맡깁니다.
                SkillUtils.ActivateChain(mech.onHit, ctx, SkillTarget.ForEntity(entity), trigger: "onHit");
            }

            /* Debug helper: keep the ray visible for a short duration when tuning hit logic. */
            Debug.DrawRay(origin, direction * distance, Color.blue, 0.1f);
        }
    }
}
