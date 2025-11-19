using System;
using Systems.Stacks.Definition;

namespace Systems.Stacks
{
    public readonly struct StackKey : IEquatable<StackKey>
    {
        public readonly StackDefinition def;
        public readonly string applierName;

        public StackKey(StackDefinition defi, string applier = "The World")
        {
            def = defi;
            applierName = applier;
        }

        public bool Equals(StackKey other)
        {
            return Equals(def, other.def) && Equals(applierName,  other.applierName);
        }

        public override int GetHashCode()
        {
            return def.GetHashCode() ^ applierName.GetHashCode();
        }
    }

    public readonly struct StackStatus
    {
        public readonly int Amount;
        public readonly ushort AppliedAt;
        public readonly ushort ExpireAt;
        public StackStatus(int amounts, ushort appliedTick, ushort expireTick)
        {
            Amount = amounts;
            AppliedAt = appliedTick;
            ExpireAt = expireTick;
        }
    }
}