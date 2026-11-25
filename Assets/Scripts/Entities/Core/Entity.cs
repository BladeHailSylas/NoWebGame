using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Entities.Relations;
using System;
using JetBrains.Annotations;



using UnityEngine;

namespace NoWebGame.Entities.Core
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Data", "Assembly-CSharp", "Entity")]
    public abstract class Entity : MonoBehaviour, IEntity
    {
        public bool targetable = true;
        [CanBeNull] public Transform Owner;
        [SerializeField, Tooltip("관계 판정의 정보 출처입니다. 기존 Entity는 LegacyLayer를 사용합니다.")]
        private EntityRelationProfile relationProfile = EntityRelationProfile.LegacyLayer;

        /// <summary>소속 정보의 지정 방식. targetable 및 Owner와 독립적으로 사용합니다.</summary>
        public EntityRelationProfile RelationProfile
        {
            get => relationProfile;
            set => relationProfile = value;
        }
        public void ApplyStack(StackKey key, long tick, int amount = 1, StackMetadata metadata = default)
        {
        }

        public void RemoveStack(StackKey key, long tick, int amount = 0)
        {
        }

        public void TakeDamage(DamageData data)
        {
        }
        public void TryRemoveStack(SwitchVariable sv)
        {
            
        }

        public void Die() {}
    }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Data", "Assembly-CSharp", "EntityEvents")]
    public sealed class EntityEvents
    {
        public event Action<StackKey, int> OnStackApplied;
        public event Action<StackKey, int> OnStackRemoved;
        public event Action<DamageData> OnDamaged;

        public void RaiseStackApplied(StackKey key, int amount)
            => OnStackApplied?.Invoke(key, amount);

        public void RaiseStackRemoved(StackKey key, int amount)
            => OnStackRemoved?.Invoke(key, amount);

        public void RaiseDamaged(DamageData delta)
            => OnDamaged?.Invoke(delta);
    }

}
