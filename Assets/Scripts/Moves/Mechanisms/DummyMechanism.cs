using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "DummyMechanism", menuName = "Skills/Mechanisms/Dummy")]
    public class DummyMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(INewParams @params, Transform caster, Transform target)
        {
        }

        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not DummyParams param) return;
            Debug.Log($"Hello {ctx.Caster} {ctx.Target}");
            foreach (var followup in param.onHit)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctx.Target);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
            //Debug.Log("Dummy: OnHit FollowUps are cast");
        
            foreach (var followup in param.onExpire)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                SkillCommand cmd = new(ctx.Caster, TargetMode.TowardsEntity, new FixedVector2(ctx.Caster.position),
                    mech, followup.@params, ctx.Damage, ctx.Target);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
            //Debug.Log("Dummy: OnExpire FollowUps are cast");
        }
    }

    [Serializable]
    public class DummyParams : INewParams
    {
        public short CooldownTicks { get; private set; }
        // Range limits for this mechanism (world units).
        public float minRange;
        public float maxRange;
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
        public float MinRange => minRange / 1000;
        public float MaxRange => maxRange / 1000;
    }
}
