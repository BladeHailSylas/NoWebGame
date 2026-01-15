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
            SkillUtils.ActivateFollowUp(param.onHit, ctx);
            //Debug.Log("Dummy: OnHit FollowUps are cast");
        
            SkillUtils.ActivateFollowUp(param.onExpire, ctx);
            //Debug.Log("Dummy: OnExpire FollowUps are cast");
        }
    }

    [Serializable]
    public class DummyParams : NewParams
    {
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
    }
}
