using SkillInterfaces;
using StatsInterfaces;
using UnityEngine;
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
}

public class SkillRunner
{
    private TargetResolver _targetResolver;
    public SkillRunner(TargetResolver resolver)
    {
        _targetResolver = resolver;
    }

    public void Activate(in SkillCommand cmd)
    {
        //Enforce maximum chain depth
        //Determine target
        Transform target = cmd.Target;
        FixedVector2 anchor = cmd.CastPosition;

        if (target is null)
        {
            var req = new TargetRequest(cmd.Caster, cmd.TargetMode);
            // TODO: Later support range/mask overrides from skill data.

            var result = _targetResolver.ResolveTarget(req);
            if (!result.Found)
            {
                // No target found — silently return (placeholder behavior)
                return;
            }

            target = result.Target;
            anchor = result.Anchor;
        }

        //Debug.Log($"Let's apply damage {cmd.Damage.Attack}");
        //Execute skill mechanism
        cmd.Mech.Execute(new CastContext(cmd.Params, cmd.Caster, target,
            cmd.Damage));
        
    }
    public void Activate(in SkillCommand cmd, in DamageData data)
    {
        //Enforce maximum chain depth

        //Determine target
        Transform target = cmd.Target;
        FixedVector2 anchor = cmd.CastPosition;

        if (target is null)
        {
            var req = new TargetRequest(cmd.Caster, cmd.TargetMode);
            // TODO: Later support range/mask overrides from skill data.

            var result = _targetResolver.ResolveTarget(req);
            if (!result.Found)
            {
                // No target found — silently return (placeholder behavior)
                return;
            }

            target = result.Target;
            anchor = result.Anchor;
        }

        //Execute skill mechanism
        cmd.Mech.Execute(new CastContext(cmd.Params, cmd.Caster, target,
            data));
    }
}