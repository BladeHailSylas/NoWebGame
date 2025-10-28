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

    public Dictionary<Effects, EffectState> EffectList { get; } = new();
    public HashSet<Effects> PositiveEffects { get; } = new() { Effects.Haste, Effects.DamageBoost, Effects.ArmorBoost, Effects.APBoost, Effects.DrBoost, Effects.Invisibility, Effects.Invincible };
    public HashSet<Effects> NegativeEffects { get; } = new() { Effects.Slow, Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled, Effects.Damage };
    public HashSet<Effects> DisturbEffects { get; } = new() { Effects.Slow, Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled };
    public HashSet<Effects> CcEffects { get; } = new() { Effects.Stun, Effects.Suppressed, Effects.Root, Effects.Tumbled };

    public PlayerEffects(PlayerContext context)
    {
        _context = context;
    }

    public bool HasEffect(Effects effect) => EffectList.ContainsKey(effect);

    public void ApplyEffect(Effects effectType, GameObject effecter, float duration = float.PositiveInfinity, int amp = 0, string name = null)
    {
        if (effectType == Effects.Stack)
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

    public void Affection(Effects effectType, float duration, float amplifier = 0)
    {
        if (CcEffects.Contains(effectType))
        {
            IsMovable = false;
            _context.Logger.Info($"{effectType} applied. Player immobilised.");
            return;
        }

        if (effectType == Effects.Invincible)
        {
            IsImmune = true;
        }
    }

    public void Purify(Effects effectType)
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

            if (effectType == Effects.Invincible)
            {
                IsImmune = false;
            }
        }
    }

    public void ClearNegative()
    {
        var toRemove = new List<Effects>();
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
