using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(menuName = "Skills/Mechanisms/Projectile")]
    public class ProjectileMechanism : ObjectGeneratingMechanism
    {
        public override void Execute(CastContext ctx)
        {
            if (ctx.Params is not ProjectileParams param)
            {
                Debug.LogError("[ProjectileMechanism] Invalid parameter type.");
                return;
            }

            // 1️⃣ Spawn position
            var spawnPos = ctx.Caster.position;

            // 2️⃣ Projectile 생성
            var go = Instantiate(param.projectilePrefab, spawnPos, Quaternion.identity);
            if (!go.TryGetComponent<ProjectileEntity>(out var entity))
                return;
            // 4️⃣ Projectile 초기화
            entity.Init(
                ctx, param
            );
        }
    }

    [Serializable]
    public class ProjectileParams : INewParams
    {
        [Header("Time")]
        [SerializeField] private short cooldownTicks;
        public ushort lifeTick;

        [Header("Projectile")]
        public ProjectileEntity projectilePrefab;
        public int speed;
        public bool penetrative;

        [Header("Callbacks")]
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;

        public short CooldownTicks => cooldownTicks;
    }
}