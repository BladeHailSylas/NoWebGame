using SkillInterfaces;
using UnityEngine;
public readonly struct SkillCommand
{
    public readonly Transform Caster;
    public readonly TargetMode TargetMode;
    public readonly FixedVector2 CastPosition;
    public readonly ISkillMechanism Mech;
    public readonly ISkillParam Param;

    public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition, 
        ISkillMechanism mech, ISkillParam param)
    {
        Caster = caster;
        TargetMode = mode;
        CastPosition = castPosition;
        Mech = mech;
        Param = param;
    }
}

/*public enum TargetMode
{
    TowardsEntity, TowardsCursor, TowardsMovement, TowardsCoordinates,
}*/
public class SkillRunner
{
    
}