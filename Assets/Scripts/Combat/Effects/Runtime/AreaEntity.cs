using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Presentation;
using global::NoWebGame.Combat.Targeting.Anchors;
using global::NoWebGame.Entities.Core;
using global::NoWebGame.Moves;
using global::NoWebGame.Moves.ObjectEntity;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Spatial;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Systems.Time;
using System.Collections.Generic;







using UnityEngine;

namespace NoWebGame.Combat.Effects.Runtime
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.ObjectEntity", "Assembly-CSharp", "AreaEntity")]
    public class AreaEntity : ObjectPrefab
    {
        [SerializeReference] public IAreaShapes areaShape;
        private const byte ActivateTick = 15;
        private ushort _limitTick;
        private ushort _lifeTick;
        private ushort _tickElapsed;
        private List<MechanismData> _onInterval;
        private List<MechanismData> _onExpire;
        private FixedVector2 _location;
        private CastContext _ctx;
        private Ticker _ticker;
        private bool _expired;

        private void OnEnable()
        {
            if (_ticker == null || _expired) return;
            _ticker.OnTick -= TickHandler;
            _ticker.OnTick += TickHandler;
        }

        private void OnDisable()
        {
            if (_ticker != null) _ticker.OnTick -= TickHandler;
        }
        public void Init(CastContext ctx)
        {
            if(ctx.Mech is not AreaMechanism area) return;
            if (!SpatialExecution.TryPrepare(ctx, out ctx, out _)) { Destroy(gameObject); return; }
            _ctx = ctx;
            AttackLog.Write(ctx.Trace, "AreaStarted", $"shape={areaShape?.GetType().Name} position={transform.position} queryLayers={(area.Targeting.IsRelative ? TargetPolicy.CandidateLayers : ctx.Mech.Mask.value)} relations={area.Targeting.Relations} lifeTicks={area.lifeTick}");
            _onInterval = area.onEnter;
            _onExpire = area.onExpire;
            _limitTick = area.lifeTick;
            _location = new FixedVector2(transform.position);
            switch (areaShape)
            {
                case LaserArea laser:
                {
                    laser.SetMaxRange((int)(area.MaxRange * 1000));
                    // 1. Start = 시전자 위치
                    var start = new FixedVector2(ctx.Caster.transform.position);

                    // 2. Target 방향 계산
                    if (!ctx.TryGetSpatialPosition(out var targetPos)) { Destroy(gameObject); return; }
                    var dir = (targetPos - start.AsVector2).normalized;

                    // 3. 거리 제한
                    var distance = Vector2.Distance(start.AsVector2, targetPos);
                    var height = Mathf.Min(distance, laser.MaxRange / 1000f);

                    // 4. End 계산
                    var endVec = start.AsVector2 + dir * height;
                    var end = new FixedVector2(endVec);
                    // 5. Laser 기하 확정
                    laser.ResolveFromContext(start, end);
                    break;
                }
                case BoxArea box:
                    box.SetCenter(transform.position);
                    box.RotationZ = transform.rotation.eulerAngles.z;
                    break;
            }

            ActivateInterval();
            if (_limitTick < ActivateTick)
            {
                Expire();
            }
            else
            {
                _ticker = global::NoWebGame.Systems.Time.Time.Ticker;
                if (_ticker != null && isActiveAndEnabled) _ticker.OnTick += TickHandler;
            }
        }

        private void TickHandler(long tick)
        {
            _lifeTick++; _tickElapsed++;
            if (_lifeTick >= _limitTick)
            {
                Expire();
            }
            else if(_tickElapsed >= ActivateTick)
            {
                _tickElapsed = 0;
                ActivateInterval();
            }
        }

        private void ActivateInterval()
        {
            Effect();
            var policy = TargetPolicy.From(_ctx.Mech);
            var layers = policy.IsRelative ? TargetPolicy.CandidateLayers : _ctx.Mech.Mask.value;
            var seen = policy.IsRelative ? new HashSet<Entity>() : null;
            switch (areaShape)
            {
                case CircleArea circle:
                {
                    var worldCenter = _location.AsVector2;
                    var radius = circle.Radius / 1000f;
                    // 물리 감지 (Player/Enemy 등 대상 레이어 필터 적용 가능)
                    var results = Physics2D.OverlapCircleAll(worldCenter, radius, layers);
                    AttackLog.Write(_ctx.Trace, "AreaQuery", $"shape={areaShape.GetType().Name} queryLayers={layers} relations={policy.Relations} colliders={results.Length}");
                    foreach (var col in results)
                    {
                        Entity entity;
                        if (policy.IsRelative)
                        {
                            if (!RelativeHitQueries.TryAccept(_ctx, col, seen, out entity)) continue;
                        }
                        else col.TryGetComponent(out entity);
                        if (entity is null)
                        {
                            AttackLog.Hit(_ctx, col, "RejectedNoEntityOnCollider");
                            continue;
                        }
                        // OnHit FollowUp 실행
                        if (!policy.IsRelative) AttackLog.Hit(_ctx, col, "Accepted");
                        SkillUtils.ActivateChain(_onInterval, _ctx, entity.transform, trigger: "_onInterval");
                    }
                    break;
                }
                case IBoxLikeArea boxLike:
                {
                    var center = boxLike.CenterCoordinate.AsVector2;
                    var size = boxLike.GetBoxSize();
                    var angle = boxLike.GetRotation();

                    var results = Physics2D.OverlapBoxAll(center, size, angle, layers);
                    AttackLog.Write(_ctx.Trace, "AreaQuery", $"shape={areaShape.GetType().Name} queryLayers={layers} relations={policy.Relations} colliders={results.Length}");
                    foreach (var col in results)
                    {
                        Entity entity;
                        if (policy.IsRelative)
                        {
                            if (!RelativeHitQueries.TryAccept(_ctx, col, seen, out entity)) continue;
                        }
                        else col.TryGetComponent(out entity);
                        if (entity is null)
                        {
                            AttackLog.Hit(_ctx, col, "RejectedNoEntityOnCollider");
                            continue;
                        }
                        if (!policy.IsRelative) AttackLog.Hit(_ctx, col, "Accepted");
                        SkillUtils.ActivateChain(_onInterval, _ctx, entity.transform, trigger: "_onInterval");
                    }
                    break;
                }
            }
        }

        private void Expire()
        {
            if (_expired) return;
            _expired = true;
            AttackLog.Write(_ctx.Trace, "AreaEnded", $"lifeTicks={_lifeTick}");
            if (_ticker != null) _ticker.OnTick -= TickHandler;
            if (_onExpire.Count == 0)
            {
                if (_ctx.Target.TryGetComponent<SkillAnchor>(out var anchor))
                {
                    AnchorRegistry.Instance.Return(anchor);
                }
            }
            else {
                SkillUtils.ActivateChain(_onExpire, _ctx, trigger: "_onExpire");
            }
            Destroy(gameObject);
        }

        private void Effect()
        {
            if(_ctx.Mech is not AreaMechanism mech) return;
            if (mech.effectPrefab is null) return;
            var go = Instantiate(mech.effectPrefab, transform.position, Quaternion.identity);
            if (!go.TryGetComponent<AreaEffectEntity>(out var effect)) return;
            switch (areaShape)
            {
                case CircleArea circle:
                    effect.InitCircle(transform.position, circle.Radius / 1000f);
                    break;
                case BoxArea box:
                    effect.InitBox(transform.position, box.GetBoxSize(), box.RotationZ);
                    break;
                case LaserArea laser:
                    effect.transform.position = Vector3.zero;
                    effect.InitLaser(laser.Start.AsVector2, laser.End.AsVector2, laser.Width / 1000f);
                    break;
            }
        }
    }
}
