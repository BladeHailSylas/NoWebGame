using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using Systems.Data;
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

            // 3️⃣ 방향 / 속도 결정
            // TODO: TargetMode 확장 완료 시 정교화
            Vector2 dir = (ctx.Target is not null ? (ctx.Target.position - spawnPos).normalized :
                // 기본 방향 (캐릭터 전방)
                ctx.Caster.right);

            var velocity = new FixedVector2(dir * param.speed);

            // 4️⃣ Projectile 초기화
            entity.Init(
                ctx.Damage,
                param.onHit,
                param.onExpire,
                ctx.Caster,
                velocity,
                param.lifeTick
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