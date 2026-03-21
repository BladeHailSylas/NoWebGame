using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class SummonMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")]
        public SummonEntity summonPrefab;
        public List<MechanismData> onSummoned;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not SummonMechanism mech)
            {
                return;
            }
            summonPrefab.Init(ctx);
            SkillUtils.ActivateFollowUp(mech.onSummoned, ctx);
        }
    }
}
