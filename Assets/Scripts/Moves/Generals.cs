using System;
using UnityEngine;

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

/// <summary>
/// Base class for any mechanism that needs to spawn objects in the world deterministically.
/// </summary>
public abstract class ObjectGeneratingMechanism : ScriptableObject, INewMechanism
{
    /// <summary>
    /// Creates a new GameObject at the given position.
    /// Duration-based expiration will be added later.
    /// </summary>
    protected GameObject GenerateObject(string name, Vector3 position, ushort durationTicks)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;

        var collider = obj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        //collider.radius = radius;

        if (durationTicks > 0)
        {
            Debug.Log("You are making an expirable entity, but the duration is not handled now.");
            //obj.AddComponent<DeterministicLifetime>().Initialize(durationTicks);
        }
        return obj;
    }

    protected GameObject GenerateObject(string name, Vector2 position)
    {
        Debug.Log("You are using the GenerateObject method without durationTicks; it will become 0 temporarily");
        return GenerateObject(name, position, 0);
    }
    /// <summary>
    /// All mechanisms must implement this — defines their activation behavior.
    /// </summary>
    public abstract void Execute(INewParams @params, Transform caster, Transform target);
}

[Serializable]
public struct MechanismRef
{
    public ScriptableObject Mechanism;
    [SerializeReference] public INewParams Params;
    public bool PassSameTarget;
    public bool RespectBusyCooldown;
}
