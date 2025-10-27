using System.Diagnostics;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

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

public abstract class ObjectGeneratingMechanism : ScriptableObject, INewMechanism
{
    protected GameObject GenerateObject(string name, Vector3 position, float radius, int durationTicks)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;

        var collider = obj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius;

        if (durationTicks > 0)
        {
            //obj.AddComponent<DeterministicLifetime>().Initialize(durationTicks);
        }
        return obj;
    }

    public abstract void Execute(INewParams @params, Transform caster, Transform target);
}
