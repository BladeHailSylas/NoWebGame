using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class StackMechanism : NewMechanism, INewMechanism
    {
        [Header("Stack")]
        public StackDefinition stack;
        public int amount = 1;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not StackMechanism mech) return;
            if (!ctx.Target.TryGetComponent(out IStackable stacker))
            {
                return;
            }

            stacker.ApplyStack(new StackKey(mech.stack, ctx.Caster.name, ctx.Caster), 65535, mech.amount);
            SkillUtils.ActivateChain(mech.onHit, ctx);
            SkillUtils.ActivateChain(mech.onExpire, ctx);
        }
    }
}
