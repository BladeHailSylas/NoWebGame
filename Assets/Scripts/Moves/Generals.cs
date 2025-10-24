using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// Encapsulates damage configuration for hitscan or projectile skills.
/// </summary>
[System.Serializable]
public readonly struct DamageData
{
    public readonly StatsInterfaces.DamageType Type;
    public readonly int Value;
    public readonly int APRatio;

    public DamageData(StatsInterfaces.DamageType type, int value, int apRatio)
    {
        Type = type;
        Value = value;
        APRatio = apRatio;
    }
}
// DamageType Normal, Fixed, MaxPercent, CurrentPercent, LostPercent

public interface INewMechanism
{
    void Execute(INewParams @params, Transform caster, Transform target);
}

public interface INewParams
{
    short CooldownTicks { get; }
}