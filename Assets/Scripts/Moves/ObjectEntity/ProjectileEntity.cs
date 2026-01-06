using System.Collections.Generic;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.SubSystems;
using Systems.Ticker;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public class ProjectileEntity : ObjectPrefab
    {
        private const byte LifeTick = 15;

        private ushort _limitTick;
        private ushort _lifeTick;

        private DamageData _damage;
        private List<MechanismRef> _onHit;
        private List<MechanismRef> _onExpire;

        private FixedVector2 _location;
        private FixedVector2 _velocity;

        private Transform _originalCaster;
        private ThinMotor _motor;

        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D col;

        [Header("Projectile Policy")]
        [SerializeField] private bool penetrative;

        public void Init(
            DamageData dmg,
            List<MechanismRef> onHit,
            List<MechanismRef> onExpire,
            Transform caster,
            FixedVector2 velocity,
            ushort life = 60)
        {
            _damage = dmg;
            _onHit = onHit;
            _onExpire = onExpire;
            _limitTick = life;
            _velocity = velocity;

            _location = new FixedVector2(transform.position);
            _originalCaster = caster;

            // 🔹 ThinMotor 생성 (Entity 소유)
            _motor = new ThinMotor(rb, col)
            {
                wallMask = LayerMask.GetMask("Walls&Obstacles"),
                enemyMask = LayerMask.GetMask("Foe")
            };

            if (_limitTick < LifeTick)
            {
                Debug.Log("lifetime is too short (under 0.25s); instant kill");
                Expire();
                return;
            }

            Ticker.Instance.OnTick += TickHandler;
        }
        private void TickHandler(ushort tick)
        {
            if (_lifeTick >= _limitTick)
            {
                Expire();
                return;
            }
            Move();
            _lifeTick++;
        }

        /// <summary>
        /// Tick 기반 이동 처리
        /// </summary>
        private void Move()
        {
            if (!_motor.TryMove(_velocity, penetrative, out var hit))
            {
                switch (hit.type)
                {
                    case HitType.Wall:
                        // 벽에 닿으면 즉시 제거
                        Expire();
                        break;

                    case HitType.Enemy:
                        ApplyHit(hit.collider);
                        if (!penetrative)
                            Expire();
                        break;
                }
            }

            // 위치 동기화
            _location = new FixedVector2(transform.position);
        }

        private void ApplyHit(Collider2D other)
        {
            if (!other.TryGetComponent<Entity>(out var entity))
                return;

            foreach (var followup in _onHit)
            {
                if (followup.mechanism is not INewMechanism mech)
                    continue;

                SkillCommand cmd = new(
                    _originalCaster,
                    TargetMode.TowardsEntity,
                    _location,
                    mech,
                    followup.@params,
                    _damage,
                    entity.transform
                );

                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }

        private void Expire()
        {
            Ticker.Instance.OnTick -= TickHandler;

            foreach (var followup in _onExpire)
            {
                if (followup.mechanism is not INewMechanism mech)
                    continue;

                SkillCommand cmd = new(
                    _originalCaster,
                    TargetMode.TowardsEntity,
                    _location,
                    mech,
                    followup.@params,
                    _damage
                );

                CommandCollector.Instance.EnqueueCommand(cmd);
            }

            Destroy(gameObject);
        }
    }
}
