using System;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using UnityEngine;

namespace Systems.SubSystems
{
    public class Teleporter
    {
        private readonly Rigidbody2D _rb;
        private readonly Collider2D _col;

        public Teleporter(Rigidbody2D rb, Collider2D col)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _col = col ?? throw new ArgumentNullException(nameof(col));
        }

        public bool TryTeleport(TeleportContract tpc)
        {
            var destination = ResolveTeleportPoint(tpc);

            // 현재 위치와 거의 같으면 스킵
            if ((_rb.position - destination).sqrMagnitude < 0.0001f)
                return false;

            // 🔹 기존 물리 상태 제거
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            // 🔹 즉시 순간이동
            _rb.position = destination;

            // 물리 동기화 (안정성 ↑)
            Physics2D.SyncTransforms();
            CastFollowUps(tpc);
            return true;
        }

        private Vector2 ResolveTeleportPoint(
            TeleportContract tpc)
        {
            Vector2 start = tpc.Context.Caster.position;
            if (tpc.Context.Mech is not TeleportMechanism tparam)
            {
                return start;
            }
            var toTarget = (Vector2)tpc.Context.Target.position - start;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return start;
            }
            var dir = toTarget.normalized;
            var distance = Mathf.Min(toTarget.magnitude, tparam.MaxRange);
            
            var wallMask = LayerMask.GetMask("Walls&Obstacles");
            var enemyMask = LayerMask.GetMask("Foe");

            LayerMask mask = wallMask;
            if (!tparam.ignoreEnemy)
                mask |= enemyMask;

            var hit = Physics2D.Raycast(
                start,
                dir,
                distance,
                mask
            );

            if (!hit.collider)
            {
                // 아무것도 안 맞음 → 최대 이동
                return start + dir * distance;
            }

            // 충돌 발생 → 콜라이더 반경만큼 뒤로
            var skin = Mathf.Min(_col.bounds.extents.x, _col.bounds.extents.y) + 0.01f;
            return hit.point - dir * skin;
        }

        private static void CastFollowUps(TeleportContract tpc)
        {
            var ctx = tpc.Context;
            if (ctx.Mech is not TeleportMechanism param) return;
            if (param.onArrival.Count == 0)
            {
                if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                AnchorRegistry.Instance.Return(anchor);
            }
            foreach (var followup in param.onArrival)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mech, ctx.Damage, ctxTarget);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }
    }
}