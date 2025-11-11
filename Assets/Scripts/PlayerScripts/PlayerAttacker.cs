using System.Collections.Generic;
using SkillInterfaces;
using StatsInterfaces;
using UnityEngine;

/// <summary>
/// Coordinates player attacks by resolving skill bindings and enqueueing
/// commands to the shared command collector.
/// </summary>
public sealed class PlayerAttacker
{
    private readonly PlayerContext _context;
    private readonly CommandCollector _collector;
    private readonly Transform _caster;
    private readonly Dictionary<SkillSlot, SkillBinding> _skills;
    public PlayerAttacker(PlayerContext context, Transform caster, Dictionary<SkillSlot, SkillBinding> skills, CommandCollector collector)
    {
        _context = context;
        _caster = caster;
        _skills = skills ?? new Dictionary<SkillSlot, SkillBinding>();
        _collector = collector;

        foreach (var kvp in _skills)
        {
            var binding = kvp.Value;
            if (binding.mechanism is not INewMechanism)
            {
                _context.Logger.Error($"Invalid mechanism in slot {kvp.Key}.");
                continue;
            }

            if (binding.@params is not INewParams)
            {
                _context.Logger.Error($"Invalid params in slot {kvp.Key}.");
            }
        }

        _context.Logger.Info($"Attack controller initialised with {_skills.Count} skills.");
    }

    public void TryCast(SkillSlot slot)
    {
        if (!_skills.TryGetValue(slot, out var binding))
        {
            _context.Logger.Warn($"No skill bound to slot {slot}.");
            return;
        }

        if (binding.mechanism is not INewMechanism mech)
        {
            _context.Logger.Error($"Skill in slot {slot} has invalid mechanism.");
            return;
        }

        if (binding.@params is not INewParams param)
        {
            _context.Logger.Error($"Skill in slot {slot} has invalid params.");
            return;
        }

        var cmd = new SkillCommand(
            caster: _caster,
            mode: TargetMode.TowardsEntity,
            castPosition: FixedVector2.FromVector2(_caster.position),
            mech: mech,
            @params: param,
            damage: _context.Stats.DamageData()
        );
        //Debug.Log($"Sent Attack damage { _context.Stats.DamageData().Attack}");
        _collector?.EnqueueCommand(cmd);
        _context.Logger.Info($"Casted skill from slot {slot} ({mech.GetType().Name}).");
    }

    public void PrepareCast(SkillSlot slot)
    {
        if (!_skills.TryGetValue(slot, out var binding))
        {
            _context.Logger.Warn($"No skill bound to slot {slot}.");
            return;
        }

        if (binding.mechanism is not INewMechanism mech)
        {
            _context.Logger.Error($"Skill in slot {slot} has invalid mechanism.");
            return;
        }

        if (binding.@params is not INewParams param)
        {
            _context.Logger.Error($"Skill in slot {slot} has invalid params.");
            return;
        }

        _context.Logger.Info($"Preparing {mech.GetType().Name} (cooldown {param.CooldownTicks}).");
    }
}
