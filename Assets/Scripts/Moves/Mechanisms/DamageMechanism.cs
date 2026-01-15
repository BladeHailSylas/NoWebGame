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
            if (!ctx.Target.TryGetComponent(out IVulnerable vul))
            {
                Debug.Log($"{ctx.Target.name} is not Vulnerable");
                return;
            }
            var finalAP = 1 - (1 - ctx.Damage.APRatio) * (1 - param.defaultAPRatio / 100.0);
            var finalDA = ctx.Damage.Amplitude * (1 + param.defaultAmplitude / 100.0);
            //Debug.Log($"Now that we have {finalAP} = (1 - {ctx.Damage.APRatio}) * (1 - {param.defaultAPRatio / 100.0})");
            vul.TakeDamage(new DamageData(param.type, ctx.Damage.Attack, param.damageValue, finalAP, finalDA, ctx.Caster));
            Debug.Log($"I hit {ctx.Target.name} with DamageData({param.type}, {ctx.Damage.Attack}, {param.damageValue}, {ctx.Caster})");
            SkillUtils.ActivateFollowUp(param.onHit, ctx);
            //Debug.Log("Damage: OnHit FollowUps are cast");
        
            SkillUtils.ActivateFollowUp(param.onExpire, ctx);
            //Debug.Log("Damage: OnExpire FollowUps are casted");
        }
    }
    [Serializable]
    public class DamageParams : NewParams
    {
        public ushort damageValue;
        public byte defaultAPRatio;
        public byte defaultAmplitude;
        public DamageType type;
        // Range limits for this mechanism (world units).
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
    }
}
