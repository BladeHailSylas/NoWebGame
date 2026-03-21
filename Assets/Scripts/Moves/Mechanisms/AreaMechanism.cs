using System;
using System.Collections.Generic;
using Moves.Effects.Definitions;
using Moves.ObjectEntity;
using UnityEngine;

namespace Moves.Mechanisms
{
    [Serializable]
    public class AreaMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public AreaEntity areaPrefab;
        public List<MechanismData> onEnter;
        public List<MechanismData> onExpire;
        public AreaEffectEntity effectPrefab;
        public new void Execute(CastContext ctx)
        {
            areaPrefab.Init(ctx);
        }
    }
}
