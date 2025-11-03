using System;
using System.Collections.Generic;
using ActInterfaces;
using SkillInterfaces;
using StatsInterfaces;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageMechanism", menuName = "Skills/Mechanisms/Damage")]
public class DamageMechanism : ScriptableObject, INewMechanism
{
    public void Execute(CastContext ctx)
    {
        if (ctx.Params is not DamageParams param) return;
        if (!ctx.Target.TryGetComponent(out IVulnerable vul)) return;
        double finalAP = 1 - (1 - ctx.Damage.APRatio) * (1 - param.defaultAPRatio / 100.0);
        double finalDA = ctx.Damage.Amplitude * (1 + param.defaultAmplitude / 100.0);
        //Debug.Log($"Now that we have {finalAP} = (1 - {ctx.Damage.APRatio}) * (1 - {param.defaultAPRatio / 100.0})");
        vul.TakeDamage(new DamageData(param.type, ctx.Damage.Attack, param.damageValue, finalAP, finalDA));
        foreach (var followup in param.onHitFollowUps)
        {
            if (followup.mechanism is not INewMechanism mech) continue;
            SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                mech, followup.@params, ctx.Damage, ctx.Target);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
        //Debug.Log("Damage: OnHit FollowUps are cast");
        
        foreach (var followup in param.onExpireFollowUps)
        {
            if (followup.mechanism is not INewMechanism mech) continue;
            SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                mech, followup.@params, ctx.Damage, ctx.Target);
            CommandCollector.Instance.EnqueueCommand(cmd);
        }
        //Debug.Log("Damage: OnExpire FollowUps are casted");
    }
}
[Serializable]
public class DamageParams : INewParams
{
    public ushort damageValue;
    public byte defaultAPRatio;
    public byte defaultAmplitude;
    public DamageType type;
    public short CooldownTicks { get; private set; }
    public List<MechanismRef> onHitFollowUps;
    public List<MechanismRef> onExpireFollowUps;
}