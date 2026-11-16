using System;
using System.Collections.Generic;
using Moves.Mechanisms;
using Systems.Data;
using UnityEngine;

namespace Moves
{
    /// <summary>새 설정에만 사용하는 물리 검색입니다. 벽 차단과 관계 허용을 별도로 검사합니다.</summary>
    public static class RelativeHitQueries
    {
        public static bool TryAccept(CastContext ctx, Collider2D collider, HashSet<Entity> seen, out Entity entity)
        {
            entity = TargetPolicy.EntityOf(collider);
            if (!TargetPolicy.From(ctx.Mech).Allows(ctx.Caster, entity))
            { AttackLog.Hit(ctx, collider, "RejectedRelationOrValidity"); return false; }
            if (seen != null && !seen.Add(entity)) return false;
            AttackLog.Hit(ctx, collider, "Accepted");
            return true;
        }

        public static void Melee(CastContext ctx, MeleeMechanism mech)
        {
            if (!ctx.Target.TryGetPosition(out var target)) return;
            Vector2 origin = ctx.Caster.position;
            var direction = target - origin;
            if (mech.effectPrefab)
            {
                var effect = UnityEngine.Object.Instantiate(mech.effectPrefab, origin, Quaternion.identity);
                effect.Init(origin, mech.MaxRange, mech.angleDeg, direction.normalized);
            }
            var hits = Physics2D.OverlapCircleAll(origin, mech.MaxRange, TargetPolicy.CandidateLayers);
            AttackLog.Write(ctx.Trace, "MeleeQuery", $"queryLayers={TargetPolicy.CandidateLayers} relations={mech.Targeting.Relations} origin={origin} radius={mech.MaxRange} colliders={hits.Length}");
            var seen = new HashSet<Entity>();
            foreach (var hit in hits)
            {
                if (mech.angleDeg < 360 && Vector2.Angle(direction, (Vector2)hit.transform.position - origin) > mech.angleDeg * .5f) continue;
                if (TryAccept(ctx, hit, seen, out var entity))
                    SkillUtils.ActivateChain(mech.onHit, ctx, SkillTarget.ForEntity(entity), "onHit");
            }
            SkillUtils.ActivateChain(mech.onExpire, ctx, trigger: "onExpire");
        }

        public static void Ray(CastContext ctx, float distance, bool firstOnly, IEnumerable<MechanismData> onHit)
        {
            if (!ctx.Target.TryGetPosition(out var target)) return;
            Vector2 origin = ctx.Caster.position;
            var direction = (target - origin).normalized;
            var hits = Physics2D.RaycastAll(origin, direction, distance, TargetPolicy.CandidateLayers);
            var walls = TargetPolicy.WallLayers;
            bool IsWall(Collider2D col) => ((1 << col.gameObject.layer) & walls) != 0;
            Array.Sort(hits, (a, b) =>
            {
                var order = a.distance.CompareTo(b.distance);
                if (order != 0) return order;
                order = IsWall(b.collider).CompareTo(IsWall(a.collider));
                return order != 0 ? order : a.collider.GetInstanceID().CompareTo(b.collider.GetInstanceID());
            });
            AttackLog.Write(ctx.Trace, firstOnly ? "HitscanQuery" : "RayQuery", $"queryLayers={TargetPolicy.CandidateLayers} blockerLayers={walls} relations={TargetPolicy.From(ctx.Mech).Relations} origin={origin} direction={direction} distance={distance} colliders={hits.Length}");
            var seen = new HashSet<Entity>();
            foreach (var hit in hits)
            {
                if (IsWall(hit.collider)) { AttackLog.Hit(ctx, hit.collider, "WallBlocked"); break; }
                if (!TryAccept(ctx, hit.collider, seen, out var entity)) continue;
                SkillUtils.ActivateChain(onHit, ctx, SkillTarget.ForEntity(entity), "onHit");
                if (firstOnly) break;
            }
        }
    }
}
