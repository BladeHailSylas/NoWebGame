using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Targeting;
using System.Collections.Generic;




using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "StackMechanism")]
    [System.Serializable]
    public class StackMechanism : NewMechanism, INewMechanism
    {
        [Header("Stack")]
        public StackDefinition stack;
        public int amount = 1;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        protected override void ExecuteCore(CastContext ctx)
        {
            if (!TargetPolicy.AllowEffect(ctx)) return;
            if (ctx.Mech is not StackMechanism mech) return;
            if (!ctx.Target.TryGetComponent(out IStackable stacker))
            {
                return;
            }

            stacker.ApplyStack(new StackKey(mech.stack, ctx.Caster.name, ctx.Caster), 65535, mech.amount);
            SkillUtils.ActivateChain(mech.onHit, ctx, trigger: "onHit");
            SkillUtils.ActivateChain(mech.onExpire, ctx, trigger: "onExpire");
        }
    }
}
