using global::NoWebGame.Entities.Core;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Spatial;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using System;
using System.Collections.Generic;


using UnityEngine;

namespace NoWebGame.Movement.Travel
{
    /// <summary>
    /// Projectile/Dash의 공통 이동 구간 검사입니다. 실제 Collider 형상을 Cast하고,
    /// 요청과 물리 반영을 분리하여 동일 물리 스텝 사이의 여러 Tick이 이동을 덮어쓰지 않습니다.
    /// </summary>
    public sealed class TravelMotion
    {
        public const float ArrivalTolerance = 0.001f;
        private readonly Rigidbody2D _body;
        private readonly Collider2D _collider;
        private readonly CastContext _context;
        private readonly TargetPolicy _targeting;
        private readonly int _speed;
        private readonly bool _penetrative;
        private readonly int _walls;
        private readonly ContactFilter2D _filter;
        private readonly Action<Entity> _onHit;
        private readonly HashSet<Entity> _hitEntities = new();
        private readonly List<RaycastHit2D> _casts = new();
        private readonly List<Collider2D> _overlaps = new();
        private readonly List<Contact> _contacts = new();
        private bool _pending;
        private bool _targetLossLogged;
        private TravelResult? _sweptExit;
        public Vector2 RequestedPosition { get; private set; }
        public bool AwaitingPhysics => _pending;
        public int UniqueHitCount => _hitEntities.Count;

        private readonly struct Contact
        {
            public readonly Collider2D Collider;
            public readonly float Distance;
            public Contact(Collider2D collider, float distance) { Collider = collider; Distance = distance; }
        }

        public TravelMotion(Rigidbody2D body, Collider2D collider, CastContext context, int speed,
            bool penetrative, int walls, int enemies, Action<Entity> onHit)
        {
            if (!SpatialExecution.TryPrepare(context, out context, out var failure))
                throw new ArgumentException("Unprepared travel: " + failure, nameof(context));
            _body = body; _collider = collider; _context = context; _speed = speed;
            _penetrative = penetrative; _walls = walls; _onHit = onHit;
            _targeting = TargetPolicy.From(context.Mech);
            var queryLayers = _targeting.IsRelative ? TargetPolicy.CandidateLayers : walls | enemies;
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = queryLayers,
                useTriggers = Physics2D.queriesHitTriggers };
            RequestedPosition = body.position;
            AttackLog.Write(context.Trace, "TravelStarted", $"body={AttackLog.Identity(body)} origin={body.position} target={context.Target} destination={context.Destination} source={context.Source} speed={speed} penetrative={penetrative} queryLayers={queryLayers} relations={_targeting.Relations} version={_targeting.Version} blockerLayers={walls}");
        }

        public TravelResult? Advance()
        {
            // 이미 확인된 구간 명중은 다음 물리 반영까지 유지합니다.
            if (_pending) return null;
            ObserveTargetLoss();
            if (!_context.TryGetSpatialPosition(out var target)) return Interrupted(ExitCause.TargetLost);
            var origin = _body.position;
            var offset = target - origin;
            if (_speed <= 0 && offset.sqrMagnitude > ArrivalTolerance * ArrivalTolerance)
                return Interrupted(ExitCause.MovementBlocked);
            var delta = Vector2.ClampMagnitude(offset, Mathf.Max(0, _speed) / 60f);
            var distance = delta.magnitude;
            var direction = distance > 0 ? delta / distance : Vector2.zero;

            _contacts.Clear();
            _overlaps.Clear();
            _collider.Overlap(_filter, _overlaps);
            foreach (var overlap in _overlaps) _contacts.Add(new Contact(overlap, 0));
            if (distance > 0)
            {
                _casts.Clear();
                _collider.Cast(direction, _filter, _casts, distance, true);
                foreach (var hit in _casts) _contacts.Add(new Contact(hit.collider, hit.distance));
            }
            _contacts.Sort(CompareContacts);
            _sweptExit = null;
            foreach (var contact in _contacts)
            {
                var other = contact.Collider;
                if (!other || other == _collider || other.attachedRigidbody == _body) continue;
                if (IsWall(other))
                {
                    delta = direction * Mathf.Clamp(contact.Distance, 0, distance);
                    _sweptExit = new TravelResult(ExitKind.Interrupted, ExitCause.WallBlocked, origin + delta);
                    break;
                }
                var entity = _targeting.IsRelative ? TargetPolicy.EntityOf(other) : EntityIdentity.FromCollider(other);
                if (!CanHit(entity)) continue;
                RegisterHit(entity);
                if (_context.Target.Matches(entity) || !_penetrative)
                {
                    delta = direction * Mathf.Clamp(contact.Distance, 0, distance);
                    _sweptExit = new TravelResult(ExitKind.Completed,
                        _context.Target.Matches(entity) ? ExitCause.TargetHit : ExitCause.EnemyHit, origin + delta, entity);
                    break;
                }
                // 관통 명중은 루프와 이동을 계속합니다. 같은 Entity의 다른 Collider도 종료를 만들지 않습니다.
            }
            RequestedPosition = origin + delta;
            _body.MovePosition(RequestedPosition);
            _pending = true;
            return null;
        }

        /// <summary>반드시 물리 시뮬레이션 후 호출합니다. 오래된 목표 좌표를 도착 기준으로 쓰지 않습니다.</summary>
        public TravelResult? AfterPhysics()
        {
            if (!_pending) return null;
            _pending = false;
            if (_sweptExit.HasValue)
            {
                var hit = _sweptExit.Value;
                _sweptExit = null;
                return new TravelResult(hit.Kind, hit.Cause, _body.position, hit.HitEntity);
            }
            ObserveTargetLoss();
            if (!_context.TryGetSpatialPosition(out var latestTarget)) return Interrupted(ExitCause.TargetLost);
            if ((_body.position - latestTarget).sqrMagnitude > ArrivalTolerance * ArrivalTolerance) return null;
            if (!_context.Destination.HasValue && _context.Target.Kind == TargetKind.Position)
                return new TravelResult(ExitKind.Completed, ExitCause.PositionReached, _body.position);

            // 좌표 일치로 명중을 만들지 않습니다. 지정 Entity의 실제 Collider 접촉을 확인합니다.
            _overlaps.Clear();
            _collider.Overlap(_filter, _overlaps);
            foreach (var other in _overlaps)
            {
                var entity = _targeting.IsRelative ? TargetPolicy.EntityOf(other) : EntityIdentity.FromCollider(other);
                if (CanHit(entity) && _context.Target.Matches(entity))
                {
                    RegisterHit(entity);
                    return new TravelResult(ExitKind.Completed, ExitCause.TargetHit, _body.position, entity);
                }
            }
            if (_context.Destination.HasValue)
                return new TravelResult(ExitKind.Completed, ExitCause.DestinationReached, _body.position);
            // 거부된 Entity에 도착해도 명중/종료를 만들지 않습니다. 기존 수명/취소 계약이 끝냅니다.
            if (_targeting.IsRelative && !_targeting.Allows(_context.Caster, _context.Target.Entity)) return null;
            return Interrupted(ExitCause.TargetNotHit);
        }

        private void ObserveTargetLoss()
        {
            if (!_targetLossLogged && _context.Destination.HasValue && _context.Target.Kind == TargetKind.Entity && !_context.Target.IsValid)
            {
                _targetLossLogged = true;
                AttackLog.Write(_context.Trace, "FixedDestinationTargetLost", $"source={_context.Source} target={_context.Target} destination={_context.Destination}");
            }
        }
        private void RegisterHit(Entity entity)
        {
            if (_hitEntities.Add(entity)) { AttackLog.Write(_context.Trace, "TravelHit", $"entity={AttackLog.Identity(entity)} uniqueHits={_hitEntities.Count}"); _onHit?.Invoke(entity); }
        }
        private bool CanHit(Entity entity)
        {
            if (!_targeting.IsRelative) return EntityIdentity.IsValid(entity) && !IsSelf(entity);
            // Dash 본체의 자기 접촉은 효과가 아닙니다. Projectile의 Caster는 Self Mask로 판단합니다.
            if (entity && entity == _collider.GetComponentInParent<Entity>(true)) return false;
            return _targeting.Allows(_context.Caster, entity);
        }
        private bool IsSelf(Entity entity) => entity == _collider.GetComponentInParent<Entity>() ||
            (_context.Caster && entity == _context.Caster.GetComponentInParent<Entity>());
        private bool IsWall(Collider2D other) => ((_walls >> other.gameObject.layer) & 1) != 0;
        private TravelResult Interrupted(ExitCause cause) => new(ExitKind.Interrupted, cause, _body.position);
        private int CompareContacts(Contact a, Contact b)
        {
            var distance = a.Distance.CompareTo(b.Distance);
            if (distance != 0) return distance;
            var wall = IsWall(b.Collider).CompareTo(IsWall(a.Collider));
            return wall != 0 ? wall : a.Collider.GetInstanceID().CompareTo(b.Collider.GetInstanceID());
        }
    }
}
