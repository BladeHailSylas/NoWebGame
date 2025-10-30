using ActInterfaces;
using EffectInterfaces;
using StatsInterfaces;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight effect tracker responsible for storing crowd-control state and
/// exposing simple queries for other modules.
/// </summary>
public sealed class PlayerEffects : IAffectable, IEffectStats
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

    public PlayerEffects(PlayerContext context)
    {
        _context = context;
    }

    public bool HasEffect(EffectType effectType) => EffectList.ContainsKey(effectType);

    public void ApplyEffect(EffectType effectType, GameObject effecter, float duration = float.PositiveInfinity, int amp = 0, string name = null)
    {
        if (effectType == EffectType.Stack)
        {
            ApplyStack(name, amp, effecter);
            return;
        }

        if (EffectList.TryGetValue(effectType, out var state))
        {
            state.duration = Mathf.Max(state.duration, duration);
            state.amplifier += amp;
            EffectList[effectType] = state;
        }
        else
        {
            EffectList[effectType] = new EffectState(duration, amp, effecter);
        }

        Affection(effectType, duration, amp);
    }

    public void ApplyStack(string name, int amp, GameObject go)
    {
        if (string.IsNullOrEmpty(name))
        {
            _context.Logger.Warn("Attempted to apply a stack without a valid name.");
            return;
        }

        if (_stacks.TryGetValue(name, out var stack))
        {
            stack.amplifier += Mathf.Max(1, amp);
            _stacks[name] = stack;
        }
        else
        {
            _stacks[name] = new EffectState(name, Mathf.Max(1, amp), go);
        }
    }

    public void Affection(EffectType effectType, float duration, float amplifier = 0)
    {
        if (CcEffects.Contains(effectType))
        {
            IsMovable = false;
            _context.Logger.Info($"{effectType} applied. Player immobilised.");
            return;
        }

        if (effectType == EffectType.Invincible)
        {
            IsImmune = true;
        }
    }

    public void Purify(EffectType effectType)
    {
        if (EffectList.Remove(effectType))
        {
            if (CcEffects.Contains(effectType))
            {
                IsMovable = true;
                foreach (var remaining in EffectList.Keys)
                {
                    if (CcEffects.Contains(remaining))
                    {
                        IsMovable = false;
                        break;
                    }
                }
            }

            if (effectType == EffectType.Invincible)
            {
                IsImmune = false;
            }
        }
    }

    public void ClearNegative()
    {
        var toRemove = new List<EffectType>();
        foreach (var entry in EffectList)
        {
            if (DisturbEffects.Contains(entry.Key))
            {
                toRemove.Add(entry.Key);
            }
        }

        foreach (var effect in toRemove)
        {
            Purify(effect);
        }
    }
}
