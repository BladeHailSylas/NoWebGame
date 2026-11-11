using System.Collections.Generic;
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
            if (!ctx.Target.TryGetPosition(out var target)) return;
            var dist = target - origin;
            var direction = dist.normalized;
            var distance = dist.magnitude * mech.rangeMultiplier;

            // Detect both hostile targets and blockers in distance order.
            var hits = Physics2D.RaycastAll(origin, direction, distance, LayerMask.GetMask("Foe", "Walls&Obstacles"));
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            // 여러 자식 Collider에 맞아도 같은 Entity의 Chain은 한 번만 실행합니다.
            var hitEntities = new HashSet<Entity>();

            foreach (var hit in hits)
            {
                var layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Walls&Obstacles"))
                    break;

                var entity = EntityIdentity.FromCollider(hit.collider);
                if (!EntityIdentity.IsValid(entity) || !hitEntities.Add(entity)) continue;

                // 재탐색 및 ExitPosition 우선순위는 공용 Chain 처리에 맡깁니다.
                SkillUtils.ActivateChain(mech.onHit, ctx, SkillTarget.ForEntity(entity));
            }

            /* Debug helper: keep the ray visible for a short duration when tuning hit logic. */
            Debug.DrawRay(origin, direction * distance, Color.blue, 0.1f);
        }
    }
}
