using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "StackMechanism", menuName = "Skills/Mechanisms/Stack")]
    public class StackMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not StackParams param) return;
            if (!ctx.Target.TryGetComponent(out IStackable stacker))
            {
                return;
            }
            stacker.ApplyStack(new StackKey(param.stack, ctx.Caster.name, ctx.Caster), 65535, param.amount);
            SkillUtils.ActivateFollowUp(param.onHit, ctx);
            //Debug.Log("Damage: OnHit FollowUps are cast");
        
            SkillUtils.ActivateFollowUp(param.onExpire, ctx);
            //Debug.Log("Damage: OnExpire FollowUps are casted");
        }
    }
    [Serializable]
    public class StackParams : NewParams
    {
        public StackDefinition stack;

        public int amount = 1;
        // Range limits for this mechanism (world units).
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
    }
}