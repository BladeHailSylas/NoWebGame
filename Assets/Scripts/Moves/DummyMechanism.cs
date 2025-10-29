using System;
using System.Collections.Generic;
using SkillInterfaces;
using UnityEngine;

[CreateAssetMenu(fileName = "DummyMechanism", menuName = "Skills/Mechanisms/Dummy")]
public class DummyMechanism : ScriptableObject, INewMechanism
{
    public void Execute(INewParams @params, Transform caster, Transform target)
    {
    }

    public void Execute(CastContext ctx)
    {
        if (ctx.Params is not DummyParams param) return;
        //Debug.Log($"I live! {ctx.Caster}, {ctx.Target}, do you see me?");
        foreach (var followup in param.onHitFollowUps)
        {
            if (followup.mechanism is not INewMechanism mech) continue;
            SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                mech, followup.@params, ctx.Damage, ctx.Target);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
        //Debug.Log("Dummy: OnHit FollowUps are casted");
        
        foreach (var followup in param.onExpireFollowUps)
        {
            if (followup.mechanism is not INewMechanism mech) continue;
            SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                mech, followup.@params, ctx.Damage, ctx.Target);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
        //Debug.Log("Dummy: OnExpire FollowUps are casted");
    }
}

[Serializable]
public class DummyParams : INewParams
{
    public short CooldownTicks { get; private set; }
    public List<MechanismRef> onHitFollowUps;
    public List<MechanismRef> onExpireFollowUps;
}