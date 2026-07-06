using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class DummyMechanism : NewMechanism, INewMechanism
    {
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not DummyMechanism param) return;
            Debug.Log($"{ctx.Caster} casted a skill towards {ctx.Target}");
            SkillUtils.ActivateChain(param.onHit, ctx);
            //Debug.Log("Dummy: OnHit FollowUps are cast");
        
            SkillUtils.ActivateChain(param.onExpire, ctx);
            //Debug.Log("Dummy: OnExpire FollowUps are cast");
        }
    }
}
