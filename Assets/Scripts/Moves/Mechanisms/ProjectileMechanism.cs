using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    [Serializable]
    public class ProjectileMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Projectile")]
        public ProjectileEntity projectilePrefab;
        public int speed;
        public bool penetrative;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not ProjectileMechanism mech)
            {
                return;
            }

            projectilePrefab.Init(ctx);
        }
    }
}
