using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "StackMechanism", menuName = "Skills/Mechanisms/Stack")]
    public class StackMechanism : NewMechanism
    {
        [Header("Stack")]
        public StackDefinition stack;
        public int amount = 1;

        [Header("Callbacks")]
        public List<SkillData> onHit;
        public List<SkillData> onExpire;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not StackMechanism mech) return;
            if (!ctx.Target.TryGetComponent(out IStackable stacker))
            {
                return;
            }

            stacker.ApplyStack(new StackKey(mech.stack, ctx.Caster.name, ctx.Caster), 65535, mech.amount);
            SkillUtils.ActivateFollowUp(mech.onHit, ctx);
            SkillUtils.ActivateFollowUp(mech.onExpire, ctx);
        }
    }
}
