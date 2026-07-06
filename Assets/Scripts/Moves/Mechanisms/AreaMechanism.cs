using System;
using System.Collections.Generic;
using Moves.Effects.Definitions;
using Moves.ObjectEntity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moves.Mechanisms
{
    [Serializable]
    public class AreaMechanism : NewMechanism, INewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public AreaEntity areaPrefab;
        [SerializeReference] public List<MechanismData> onEnter;
        [SerializeReference] public List<MechanismData> onExpire;
        public AreaEffectEntity effectPrefab;
        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not AreaMechanism mech) return;
            var prefab = mech.areaPrefab;

            var instance = Object.Instantiate(
                prefab,
                ctx.Caster.position,
                Quaternion.identity
            );

            instance.Init(ctx);
        }
    }
}
