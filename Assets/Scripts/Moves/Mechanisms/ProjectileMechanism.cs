using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(menuName = "Skills/Mechanisms/Projectile")]
    public class ProjectileMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Projectile")]
        public ProjectileEntity projectilePrefab;
        public int speed;
        public bool penetrative;

        [Header("Callbacks")]
        public List<SkillData> onHit;
        public List<SkillData> onExpire;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not ProjectileMechanism mech)
            {
                return;
            }

            var spawnPos = ctx.Caster.position;
            var go = Instantiate(mech.projectilePrefab, spawnPos, Quaternion.identity);
            if (!go.TryGetComponent<ProjectileEntity>(out var entity))
                return;

            // The projectile reads the originating mechanism through CastContext.
            entity.Init(ctx);
        }
    }
}
