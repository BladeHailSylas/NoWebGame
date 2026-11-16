using System;
using System.Collections.Generic;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [Serializable]
    public class DamageMechanism : NewMechanism, INewMechanism
    {
        public ushort damageValue;
        public byte defaultAPRatio;
        public byte defaultAmplitude;
        public DamageType type;
        // Range limits for this mechanism (world units).
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;
        public new void Execute(CastContext ctx)
        {
            if (!TargetPolicy.AllowEffect(ctx)) return;
            var damageTrace = ctx.Trace.Damage();
            if (!ctx.Target.TryGetComponent(out IVulnerable vul))
            {
                AttackLog.Write(damageTrace, "DamageRejected", $"reason=NoValidVulnerable target={ctx.Target}");
                return;
            }
            var finalAP = 1 - (1 - ctx.Damage.APRatio) * (1 - defaultAPRatio / 100.0);
            var finalDA = ctx.Damage.Amplitude * (1 + defaultAmplitude / 100.0);
            //Debug.Log($"Now that we have {finalAP} = (1 - {ctx.Damage.APRatio}) * (1 - {param.defaultAPRatio / 100.0})");
            AttackLog.Write(damageTrace, "DamageDelivered", $"target={ctx.Target} receiver={vul.GetType().Name} type={type} attackPower={ctx.Damage.Attack} value={damageValue} ap={finalAP} amplitude={finalDA}");
            vul.TakeDamage(new DamageData(type, ctx.Damage.Attack, damageValue, finalAP, finalDA, ctx.Caster, damageTrace, Targeting.IsRelative));
            AttackLog.Write(damageTrace, "DamageReceiverReturned", "hpChange=SeeDamageAppliedOrReceiverImplementation");
            //Debug.Log($"I hit {ctx.Target.name} with DamageData({param.type}, {ctx.Damage.Attack}, {param.damageValue}, {ctx.Caster})");
            SkillUtils.ActivateChain(onHit, ctx, trigger: "onHit");
            //Debug.Log("Damage: OnHit FollowUps are cast");
        
            SkillUtils.ActivateChain(onExpire, ctx, trigger: "onExpire");
            //Debug.Log("Damage: OnExpire FollowUps are casted");
        }
    }
}
