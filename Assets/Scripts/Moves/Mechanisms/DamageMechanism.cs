using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class DamageMechanism : NewMechanism
    {
        public ushort damageValue;
        public byte defaultAPRatio;
        public byte defaultAmplitude;
        public DamageType type;
        // Range limits for this mechanism (world units).
        public List<SkillData> onHit;
        public List<SkillData> onExpire;
        public new void Execute(CastContext ctx)
        {
            if (!ctx.Target.TryGetComponent(out IVulnerable vul))
            {
                Debug.Log($"{ctx.Target.name} is not Vulnerable");
                return;
            }
            var finalAP = 1 - (1 - ctx.Damage.APRatio) * (1 - defaultAPRatio / 100.0);
            var finalDA = ctx.Damage.Amplitude * (1 + defaultAmplitude / 100.0);
            //Debug.Log($"Now that we have {finalAP} = (1 - {ctx.Damage.APRatio}) * (1 - {param.defaultAPRatio / 100.0})");
            vul.TakeDamage(new DamageData(type, ctx.Damage.Attack, damageValue, finalAP, finalDA, ctx.Caster));
            //Debug.Log($"I hit {ctx.Target.name} with DamageData({param.type}, {ctx.Damage.Attack}, {param.damageValue}, {ctx.Caster})");
            SkillUtils.ActivateFollowUp(onHit, ctx);
            //Debug.Log("Damage: OnHit FollowUps are cast");
        
            SkillUtils.ActivateFollowUp(onExpire, ctx);
            //Debug.Log("Damage: OnExpire FollowUps are casted");
        }
    }
}
