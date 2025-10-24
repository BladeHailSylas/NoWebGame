using SkillInterfaces;
using UnityEngine;
public readonly struct SkillCommand
{
    public readonly Transform Caster;
    public readonly Transform Target;
    public readonly TargetMode TargetMode;
    public readonly FixedVector2 CastPosition;
    public readonly INewMechanism Mech;
    public readonly INewParams Params;
    public readonly byte ChainDepth;

    public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
        INewMechanism mech, INewParams @params, Transform target = null, byte chainDepth = 0)
    {
        Caster = caster;
        Target = target;
        
        TargetMode = mode;
        CastPosition = castPosition;
        Mech = mech;
        Params = @params;
        ChainDepth = chainDepth;
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
        if (cmd.ChainDepth > 10)
            return;

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
        cmd.Mech.Execute(cmd.Params, cmd.Caster, target);
    }
}