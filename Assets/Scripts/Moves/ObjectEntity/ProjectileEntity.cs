using System;
using System.Collections.Generic;
using Moves.Mechanisms;
using Systems.Anchor;
using Systems.Data;
using Systems.SubSystems;
using Systems.Time;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public class ProjectileEntity : ObjectPrefab
    {
        private const byte LifeTick = 15;

        private ushort _limitTick;
        private ushort _lifeTick;
        
        private List<MechanismRef> _onHit;
        private List<MechanismRef> _onExpire;

        private FixedVector2 _location;
        private FixedVector2 _velocity;
        private int _speed;

        private HashSet<Entity> _hitEntities;
        private ThinMotor _motor;
        private bool _penetrative;
        private CastContext _ctx;
        
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D col;
        

        public void Init(CastContext ctx)
        {
            if (ctx.Params is not ProjectileParams param) return;
            _ctx = ctx;
            _onHit = param.onHit;
            _onExpire = param.onExpire;
            _limitTick = param.lifeTick;
            _penetrative = param.penetrative;
            _hitEntities = new HashSet<Entity>();

            _location = new FixedVector2(transform.position);
            _speed = param.speed;
            // 🔹 ThinMotor 생성 (Entity 소유)
            _motor = new ThinMotor(rb, col)
            {
                wallMask = LayerMask.GetMask("Walls&Obstacles"),
                enemyMask = LayerMask.GetMask("Foe")
            };

            if (_limitTick < LifeTick)
            {
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
            if (_ctx.Target is null)
            {
                //Instant kill
                Expire();
                return;
            }
            _velocity = new FixedVector2(((Vector2)_ctx.Target.transform.position - _location.AsVector2).normalized * _speed);
            _motor.TryMove(_velocity, _penetrative, out var hit);
            {
                switch (hit.type)
                {
                    case HitType.Wall:
                        // 벽에 닿으면 즉시 제거
                        Expire();
                        break;
                    case HitType.Enemy:
                    {
                        ApplyHit(hit.collider);
                        if (!_penetrative || hit.collider.transform == _ctx.Target)
                            Expire();
                        break;
                    }
                    case HitType.None:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // 위치 동기화
            _location = new FixedVector2(transform.position);
            if ((transform.position  - _ctx.Target.position).magnitude < 0.1f)
            {
                Expire();
            }
        }

        private void ApplyHit(Collider2D other)
        {
            if (!other.TryGetComponent<Entity>(out var entity)) return;
            if (_hitEntities.Contains(entity)) return;
            SkillUtils.ActivateFollowUp(_onHit, _ctx, entity.transform);
            _hitEntities.Add(entity);
        }

        private void Expire()
        {
            Ticker.Instance.OnTick -= TickHandler;
            if (_onExpire.Count == 0)
            {
                if (_ctx.Target.TryGetComponent<SkillAnchor>(out var anchor))
                {
                    AnchorRegistry.Instance.Return(anchor);
                }
            }
            SkillUtils.ActivateFollowUp(_onExpire, _ctx);
            Destroy(gameObject);
        }
    }
}
