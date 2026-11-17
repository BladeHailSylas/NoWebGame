using System;
using Moves;
using Systems.Data;
using Systems.SubSystems;
using UnityEngine;

namespace PlayerScripts.Skills
{
    /// <summary>
    /// AI가 선택한 루트 공격 대상의 요청별 스냅샷입니다.
    /// 대상이 소실되어도 미지정 입력으로 바꾸지 않아 커서 탐색으로 되돌아가지 않습니다.
    /// </summary>
    public sealed class ExplicitAttackTarget
    {
        public AttackTrace Trace { get; }
        public Entity Caster { get; }
        public Entity Target { get; }
        public TargetRelationMask Relations { get; }
        private readonly Func<bool> _isCurrent;

        public ExplicitAttackTarget(Entity caster, Entity target, TargetRelationMask relations,
            Func<bool> isCurrent = null)
        {
            Trace = AttackTrace.Root(caster ? caster.transform : null);
            Caster = caster;
            Target = target;
            Relations = relations;
            _isCurrent = isCurrent;
        }

        public bool IsValid(Transform caster, INewMechanism mechanism) => Validate(caster, mechanism) == "Allowed";
        public string Validate(Transform caster, INewMechanism mechanism)
        {
            if (_isCurrent != null && !_isCurrent()) return "RequestLifetimeEnded";
            if (!Caster) return "CasterMissing";
            if (caster != Caster.transform) return "CasterMismatch";
            if (mechanism == null) return "MechanismMissing";
            var reason = EntityAttackSelection.Validate(Caster, Target, Relations, mechanism.MinRange, mechanism.MaxRange);
            if (reason != "Allowed") return reason;
            var policy = TargetPolicy.From(mechanism);
            return !policy.IsRelative || policy.Allows(caster, Target) ? "Allowed" : "MechanismRelationMaskMismatch";
        }
        public string Describe(INewMechanism mechanism) => $"target={AttackLog.Identity(Target)} selectionRelations={Relations} selectionRelation={EntityRelationResolver.Resolve(Caster, Target)} casterPosition={(Caster ? Caster.transform.position.ToString() : "missing")} targetPosition={(Target ? Target.transform.position.ToString() : "missing")} distance={(Caster && Target ? Vector2.Distance(Caster.transform.position, Target.transform.position) : -1)} min={mechanism?.MinRange} max={mechanism?.MaxRange}";
    }

    /// <summary>임시 AI의 탐색/실행 전 검증이 같은 조건을 사용합니다. 기존 물리 Mask는 변환하지 않습니다.</summary>
    public static class EntityAttackSelection
    {
        public static bool IsAllowed(Entity caster, Entity target, TargetRelationMask mask, float min, float max) =>
            Validate(caster, target, mask, min, max) == "Allowed";
        public static string Validate(Entity caster, Entity target, TargetRelationMask mask, float min, float max)
        {
            if (!caster) return "CasterMissing";
            if (!caster.isActiveAndEnabled) return "CasterInactive";
            if (!target) return "TargetMissing";
            if (!target.isActiveAndEnabled) return "TargetInactive";
            if (!target.targetable) return "TargetNotTargetable";
            if (!ValidRange(min, max)) return "InvalidRange";
            var relation = EntityRelationResolver.Resolve(caster, target);
            if (!TargetRelationMasks.Allows(mask, relation)) return relation == EntityRelation.Unknown ? "RelationUnknown" : "RelationMaskMismatch";
            var distance = ((Vector2)target.transform.position - (Vector2)caster.transform.position).sqrMagnitude;
            if (distance < min * min) return "BelowMinRange";
            return distance <= max * max ? "Allowed" : "BeyondMaxRange";
        }

        public static bool ValidRange(float min, float max) => min >= 0 && max >= min &&
            !float.IsNaN(min) && !float.IsInfinity(min) && !float.IsNaN(max) && !float.IsInfinity(max);

        public static Entity FindNearest(Entity caster, TargetRelationMask mask, float min, float max)
        {
            if (!caster || !caster.isActiveAndEnabled || !ValidRange(min, max) ||
                !TargetRelationMasks.IsValid(mask) || mask == TargetRelationMask.None) return null;
            // 자식 Collider의 Layer와 부모 Entity의 소속은 다를 수 있으므로 후보를 진영으로 좁히지 않습니다.
            var filter = new ContactFilter2D { useLayerMask = false, useTriggers = true };
            var colliders = new System.Collections.Generic.List<Collider2D>();
            Physics2D.OverlapCircle(caster.transform.position, max, filter, colliders);
            var seen = new System.Collections.Generic.HashSet<Entity>();
            Entity best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var collider in colliders)
            {
                var entity = collider.GetComponentInParent<Entity>(true);
                if (!entity || !seen.Add(entity) || !IsAllowed(caster, entity, mask, min, max)) continue;
                var distance = ((Vector2)entity.transform.position - (Vector2)caster.transform.position).sqrMagnitude;
                if (distance < bestDistance || (distance == bestDistance &&
                    (!best || entity.GetInstanceID() < best.GetInstanceID())))
                {
                    best = entity;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
