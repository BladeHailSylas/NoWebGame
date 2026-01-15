using System;
using JetBrains.Annotations;
using Moves;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Systems.Data
{
    public abstract class Entity : MonoBehaviour, IEntity
    {
        public bool targetable = true;
        [CanBeNull] public Transform Owner;
        public void ApplyStack(StackKey key, ushort tick, int amount = 1, StackMetadata metadata = default)
        {
        }

        public void RemoveStack(StackKey key, ushort tick, int amount = 0)
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