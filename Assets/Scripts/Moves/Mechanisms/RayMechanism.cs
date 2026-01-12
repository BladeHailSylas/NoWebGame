using System.Collections.Generic;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "RayMechanism", menuName = "Skills/Mechanisms/Ray")]
    public class RayMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not RayParams param) return;
            Vector2 origin = ctx.Caster.position;
            Vector2 target = ctx.Target.position;
            var dist = target - origin;
            var direction = dist.normalized;
            var distance = dist.magnitude * param.rangeMultiplier;

            // 감지: 적과 장애물 모두 탐색
            var hits = Physics2D.RaycastAll(origin, direction, distance, LayerMask.GetMask("Foe", "Walls&Obstacles"));
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var layer = hit.collider.gameObject.layer;

                // 벽을 만나면 중단
                if (layer == LayerMask.NameToLayer("Walls&Obstacles"))
                    break;

                // 적 판정
                if (!hit.collider.TryGetComponent<Entity>(out var entity)) continue;

                // FollowUp 즉시 발동
                foreach (var followup in param.onHit)
                {
                    if (param.onHit.Count == 0)
                    {
                        if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                        AnchorRegistry.Instance.Return(anchor);
                    }
                    if (followup.mechanism is not INewMechanism mech) continue;
                    var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                    SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                        mech, followup.@params, ctx.Damage, ctxTarget);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }

            // 시각 효과 (디버그용)
            Debug.DrawRay(origin, direction * distance, Color.blue, 0.1f);
        }
    }

    [System.Serializable]
    public class RayParams : NewParams
    {
        public float rangeMultiplier = 1f;
        public List<MechanismRef> onHit;
    }
}
