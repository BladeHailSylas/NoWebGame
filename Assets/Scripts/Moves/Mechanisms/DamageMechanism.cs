using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "DamageMechanism", menuName = "Skills/Mechanisms/Damage")]
    public class DamageMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not DamageParams param) return;
            if (!ctx.Target.TryGetComponent(out IVulnerable vul)) return;
            var finalAP = 1 - (1 - ctx.Damage.APRatio) * (1 - param.defaultAPRatio / 100.0);
            var finalDA = ctx.Damage.Amplitude * (1 + param.defaultAmplitude / 100.0);
            //Debug.Log($"Now that we have {finalAP} = (1 - {ctx.Damage.APRatio}) * (1 - {param.defaultAPRatio / 100.0})");
            vul.TakeDamage(new DamageData(param.type, ctx.Damage.Attack, param.damageValue, finalAP, finalDA));
            foreach (var followup in param.onHit)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctxTarget);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
            //Debug.Log("Damage: OnHit FollowUps are cast");
        
            foreach (var followup in param.onExpire)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                /*SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctx.Target);*/
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, ctx.Mode, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctxTarget);
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
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
    }
}