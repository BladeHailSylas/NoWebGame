using SkillInterfaces;
using StatsInterfaces;
using UnityEngine;
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