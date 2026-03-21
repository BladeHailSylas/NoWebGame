using System.Collections.Generic;
using Moves.Effects.Definitions;
using Moves.ObjectEntity;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class AreaMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public AreaEntity areaPrefab;
        public List<SkillData> onEnter;
        public List<SkillData> onExpire;
        public AreaEffectEntity effectPrefab;
        public new void Execute(CastContext ctx)
        {
            areaPrefab.Init(ctx);
        }
    }
}
