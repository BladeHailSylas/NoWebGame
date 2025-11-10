using System;
using UnityEngine;

public readonly struct StackProperty : IEquatable<StackProperty>
{
    public readonly StackDefinition def;
    public readonly string applierName;

    public StackProperty(StackDefinition defi, string applier = "The World")
    {
        def = defi;
        applierName = applier;
    }

    public bool Equals(StackProperty other)
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
    public readonly int amount;
    public readonly ushort appliedAt;
    public readonly ushort expireAt;
    public StackStatus(int amounts, ushort appliedTick, ushort expireTick)
    {
        amount = amounts;
        appliedAt = appliedTick;
        expireAt = expireTick;
    }
}