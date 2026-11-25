using global::NoWebGame.Entities.Core;
using System;

using UnityEngine;

namespace NoWebGame.Combat.Targeting
{
    public enum TargetKind { Unresolved = 0, Entity = 1, Position = 2 }

    /// <summary>Entity는 정체성만 보관하고 위치는 조회 시 읽습니다. Position은 고정된 값입니다.</summary>
    public readonly struct SkillTarget : IEquatable<SkillTarget>
    {
        public TargetKind Kind { get; }
        public Entity Entity { get; }
        private readonly Vector2 _position;
        private readonly int _entityId;
        public bool IsResolved => Kind != TargetKind.Unresolved;
        public bool IsValid => Kind == TargetKind.Position || (Kind == TargetKind.Entity && EntityIdentity.IsValid(Entity));

        private SkillTarget(TargetKind kind, Entity entity, Vector2 position)
        {
            Kind = kind;
            Entity = entity;
            _position = position;
            _entityId = entity ? entity.GetInstanceID() : 0;
        }

        public static SkillTarget ForEntity(Entity entity) => new(TargetKind.Entity, entity, default);
        public static SkillTarget At(Vector2 position) => new(TargetKind.Position, null, position);

        // 기존 호출자의 Transform을 받는 경계 어댑터입니다. 좌표 Transform은 즉시 값으로 복사합니다.
        public static implicit operator SkillTarget(Transform value)
        {
            if (!value) return default;
            var entity = value.GetComponentInParent<Entity>();
            return entity ? ForEntity(entity) : At(value.position);
        }

        public bool TryGetPosition(out Vector2 position)
        {
            if (Kind == TargetKind.Position) { position = _position; return true; }
            if (Kind == TargetKind.Entity && EntityIdentity.IsValid(Entity))
            { position = Entity.transform.position; return true; }
            position = default;
            return false;
        }

        public bool TryGetComponent<T>(out T component)
        {
            component = default;
            return Kind == TargetKind.Entity && EntityIdentity.IsValid(Entity) && Entity.TryGetComponent(out component);
        }

        public bool TryGetComponent(Type type, out Component component)
        {
            component = null;
            return Kind == TargetKind.Entity && EntityIdentity.IsValid(Entity) && Entity.TryGetComponent(type, out component);
        }

        public bool Matches(Entity entity) => Kind == TargetKind.Entity && entity && Entity == entity;
        public bool Equals(SkillTarget other) => Kind == other.Kind &&
            (Kind == TargetKind.Entity ? _entityId == other._entityId : Kind != TargetKind.Position || _position.Equals(other._position));
        public override bool Equals(object obj) => obj is SkillTarget other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, _entityId, _position);
        public override string ToString() => Kind == TargetKind.Entity ? $"Entity({_entityId})" : $"{Kind}({_position})";
    }

    public static class EntityIdentity
    {
        public static Entity FromCollider(Collider2D collider) => collider ? collider.GetComponentInParent<Entity>() : null;
        public static bool IsValid(Entity entity) => entity && entity.isActiveAndEnabled && entity.targetable;
    }
}
