using System.Collections.Generic;
using SkillInterfaces;
using UnityEngine;

public class PlayerAttackController
{
    private readonly SkillRunner _runner;
    private readonly Transform _caster;
    private readonly Dictionary<SkillSlot, SkillBinding> _skills;

    public PlayerAttackController(Transform caster, TargetResolver resolver, Dictionary<SkillSlot, SkillBinding> skills)
    {
        _caster = caster;
        _runner = new SkillRunner(resolver);
        _skills = skills ?? new Dictionary<SkillSlot, SkillBinding>();

        // Validate all provided skill bindings
        foreach (var kvp in _skills)
        {
            var binding = kvp.Value;

            if (binding.mechanism is not INewMechanism)
            {
                Debug.LogError($"[PlayerAttackController] Invalid mechanism in slot {kvp.Key}.");
                continue;
            }

            if (binding.@params is not INewParams)
            {
                Debug.LogError($"[PlayerAttackController] Invalid params in slot {kvp.Key}.");
            }
        }

        Debug.Log($"[PlayerAttackController] Initialized with {_skills.Count} bound skills.");
    }

    /// <summary>
    /// Attempts to cast the skill assigned to the given slot.
    /// </summary>
    public void TryCast(SkillSlot slot)
    {
        if (!_skills.TryGetValue(slot, out var binding))
        {
            Debug.LogWarning($"[PlayerAttackController] No skill bound to slot {slot}.");
            return;
        }

        if (binding.mechanism is not INewMechanism mech)
        {
            Debug.LogError($"[PlayerAttackController] Skill in slot {slot} has invalid mechanism.");
            return;
        }

        if (binding.@params is not INewParams param)
        {
            Debug.LogError($"[PlayerAttackController] Skill in slot {slot} has invalid params.");
            return;
        }

        // Construct the skill command
        var cmd = new SkillCommand(
            caster: _caster,
            mode: TargetMode.TowardsEntity,
            castPosition: FixedVector2.FromVector2(_caster.position),
            mech: mech,
            @params: param,
            target: null,
            chainDepth: 0
        );

        // Trigger the skill
        _runner.Activate(cmd);
        Debug.Log($"[PlayerAttackController] Casted skill from slot {slot} ({mech.GetType().Name}).");
    }
}