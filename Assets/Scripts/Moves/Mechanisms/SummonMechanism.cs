using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class SummonMechanism : NewMechanism, INewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")]
        [SerializeReference] public SummonEntity summonPrefab;
        [SerializeReference] public List<MechanismData> onSummoned;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not SummonMechanism mech)
            {
                return;
            }
            var prefab = mech.summonPrefab;

            var instance = Object.Instantiate(
                prefab,
                ctx.Caster.position,
                Quaternion.identity
            );

            instance.Init(ctx);
        }
    }
}
