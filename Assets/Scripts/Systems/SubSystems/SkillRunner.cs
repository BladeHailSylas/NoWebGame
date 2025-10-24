using SkillInterfaces;
using UnityEngine;
public readonly struct SkillCommand
{
    public readonly Transform Caster;
    public readonly Transform Target;
    public readonly TargetMode TargetMode;
    public readonly FixedVector2 CastPosition;
    public readonly ISkillMechanism Mech;
    public readonly ISkillParams Params;

    public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition, 
        ISkillMechanism mech, ISkillParams @params, Transform target = null)
    {
        Caster = caster;
        Target = target;
        TargetMode = mode;
        CastPosition = castPosition;
        Mech = mech;
        Params = @params;
    }
}
public class SkillRunner
{
    private TargetResolver _targetResolver;
    public SkillRunner(TargetResolver resolver)
    {
        _targetResolver = resolver;
    }
    public void Activate(in SkillCommand cmd, int chained = 0)
    {
        
    }
}