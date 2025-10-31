using ActInterfaces;
using EffectInterfaces;
using StatsInterfaces;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight effect tracker responsible for storing crowd-control state and
/// exposing simple queries for other modules.
/// </summary>
public sealed class PlayerEffect
{
    private readonly PlayerContext _context;
    private readonly Dictionary<string, EffectState> _stacks = new();

    public bool IsImmune { get; private set; }
    public bool IsMovable { get; private set; } = true;
    public bool IsAttackable { get; private set; } = true;
    public float EffectResistance { get; private set; }

    public Dictionary<EffectType, EffectState> EffectList { get; } = new();
    public HashSet<EffectType> PositiveEffects { get; } = new() { EffectType.Haste, EffectType.DamageBoost, EffectType.ArmorBoost, EffectType.APBoost, EffectType.DRBoost, EffectType.Invisibility, EffectType.Invincible };
    public HashSet<EffectType> NegativeEffects { get; } = new() { EffectType.Slow, EffectType.Stun, EffectType.Suppressed, EffectType.Root, EffectType.Tumbled, EffectType.Damage };
    public HashSet<EffectType> DisturbEffects { get; } = new() { EffectType.Slow, EffectType.Stun, EffectType.Suppressed, EffectType.Root, EffectType.Tumbled };
    public HashSet<EffectType> CcEffects { get; } = new() { EffectType.Stun, EffectType.Suppressed, EffectType.Root, EffectType.Tumbled };

    public PlayerEffect(PlayerContext context)
    {
        _context = context;
    }
}
