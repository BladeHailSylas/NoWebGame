using System;
using JetBrains.Annotations;
using Systems.Stacks.Definition;
using Systems.Time;
using UnityEngine;

namespace Systems.Stacks
{
    public readonly struct StackKey : IEquatable<StackKey>
    {
        public readonly StackDefinition def;
        public readonly string applierName;
        [CanBeNull] public readonly Transform applier;

        public StackKey(StackDefinition defi, string name = "The World", Transform who = null)
        {
            def = defi;
            applierName = name;
            applier = who;
        }

        public bool Equals(StackKey other)
        {
            return Equals(def, other.def) && Equals(applierName, other.applierName);
        }

        public override int GetHashCode()
        {
            return def.GetHashCode() ^ applierName.GetHashCode();
        }
    }

    public readonly struct StackStatus
    {
        public readonly int Amount;
        public readonly DelayId DelayId;
        public StackStatus(int amounts, DelayId id)
        {
            Amount = amounts;
            DelayId = id;
        }
    }
}