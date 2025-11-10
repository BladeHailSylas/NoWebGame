using System;
using SkillInterfaces;
using UnityEngine;

/// <summary>
/// Encapsulates damage configuration for hitscan or projectile skills.
/// </summary>
public readonly struct DamageData
{
    public readonly StatsInterfaces.DamageType Type;
    public readonly int Value;
    public readonly int Attack;
    public readonly double APRatio;
    public readonly double Amplitude;

    public DamageData(StatsInterfaces.DamageType type, int attack, int value = 1, double apRatio = 0, double amplitude = 100)
    {
        Type = type;
        Attack = attack;
        Value = value;
        APRatio = apRatio;
        Amplitude = amplitude;
    }
}
// DamageType Normal, Fixed, MaxPercent, CurrentPercent, LostPercent

public interface INewMechanism
{
    void Execute(CastContext ctx);
}

public interface ISystemMechanism : INewMechanism
{
    
}

public interface INewParams
{
    short CooldownTicks { get; }
}

public struct CastContext
{
    public readonly INewParams Params;
    public readonly Transform Caster;
    public readonly Transform Target;
    public DamageData Damage;

    public CastContext(INewParams param, Transform caster, Transform target, DamageData damage)
    {
        Params = param;
        Caster = caster;
        Target = target;
        Damage = damage;
    }
}

public readonly struct SkillCommand
{
    public readonly Transform Caster;
    public readonly Transform Target;
    public readonly TargetMode TargetMode;
    public readonly FixedVector2 CastPosition;
    public readonly INewMechanism Mech;
    public readonly INewParams Params;
    public readonly DamageData Damage;

    public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
        INewMechanism mech, INewParams @params, DamageData damage, Transform target = null)
    {
        Caster = caster;
        Target = target;
        TargetMode = mode;
        CastPosition = castPosition;
        Mech = mech;
        Params = @params;
        Damage = damage;
    }

    public SkillCommand(Transform caster1, FixedVector2 castPosition, TargetMode mode, INewMechanism mech,
        INewParams @params,
        DamageData damage, Transform caster = null, Transform target = null)
    {
        Caster = caster;
        Target = target;
        TargetMode = mode;
        CastPosition = castPosition;
        Mech = mech;
        Params = @params;
        Damage = damage;
    }
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
        var obj = new GameObject(name)
        {
            transform =
            {
                position = position
            }
        };

        //var collider = obj.AddComponent<CircleCollider2D>();
        //collider.isTrigger = true;
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
        //Debug.Log("You are using the GenerateObject method without durationTicks; it will become 0 temporarily");
        return GenerateObject(name, position, 0);
    }
    /// <summary>
    /// All mechanisms must implement this — defines their activation behavior.
    /// </summary>
    public abstract void Execute(INewParams @params, Transform caster, Transform target);

    public abstract void Execute(CastContext ctx);
}

[Serializable]
public struct MechanismRef
{
    public ScriptableObject mechanism;
    [SerializeReference] public INewParams @params;
    public bool passSameTarget;
    public bool respectBusy;
}
