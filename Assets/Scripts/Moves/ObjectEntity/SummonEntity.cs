using System.Collections.Generic;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using Systems.Anchor;
using Systems.Data;
using Systems.Stacks;
using Systems.Time;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public class SummonEntity : SummonEntityBase, IVulnerable, IStackable
    {
        public void Init(CastContext ctx)
        {
            if (ctx.Params is not SummonParams param) return;
            Debug.Log("I live");
            Owner = ctx.Caster;
            gameObject.layer = LayerMask.NameToLayer("You");
            Awaken(param);
            OnEnabled();
        }
        public new void TakeDamage(DamageData data)
        {
            _filter.FilterDamage(data);
        }

        public void ApplyStack(StackKey key, ushort tick = 65535, int amount = 1)
        {
            var delta = new StackDelta(key, amount);
            _stackManager.EnqueueStack(delta);
        }
    }
}