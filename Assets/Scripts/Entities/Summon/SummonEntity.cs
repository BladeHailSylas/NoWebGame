using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Targeting;
using System.Collections.Generic;








using UnityEngine;

namespace NoWebGame.Entities.Summon
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.ObjectEntity", "Assembly-CSharp", "SummonEntity")]
    public class SummonEntity : SummonEntityBase, IVulnerable, IStackable
    {
        public void Init(CastContext ctx)
        {
            if (ctx.Mech is not SummonMechanism param) return;
            Owner = ctx.Caster;
            if (param.Targeting.IsRelative) TargetPolicy.CopyAffiliation(ctx.Caster, this);
            else gameObject.layer = LayerMask.NameToLayer("You");
            Awaken(param);
            OnEnabled();
        }
        public new void TakeDamage(DamageData data)
        {
            _filter.FilterDamage(data);
        }

        public void ApplyStack(StackKey key, long tick = 65535, int amount = 1)
        {
            var delta = new StackDelta(key, amount);
            _stackManager.EnqueueStack(delta);
        }
    }
}