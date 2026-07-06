using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moves.Mechanisms
{
    [Serializable]
    public class ProjectileMechanism : NewMechanism, INewMechanism
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
            var prefab = mech.projectilePrefab;

            var instance = Object.Instantiate(
                prefab,
                ctx.Caster.position,
                Quaternion.identity
            );

            instance.Init(ctx);
        }
    }
}
