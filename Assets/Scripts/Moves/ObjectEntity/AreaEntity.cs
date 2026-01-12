using System.Collections.Generic;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Anchor;
using Systems.Data;
using Systems.Time;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public class AreaEntity : ObjectPrefab
    {
        [SerializeReference] public IAreaShapes areaShape;
        private const byte ActivateTick = 15;
        private ushort _limitTick;
        private ushort _lifeTick;
        private ushort _tickElapsed;
        private List<MechanismRef> _onInterval;
        private List<MechanismRef> _onExpire;
        private FixedVector2 _location;
        private CastContext _ctx;
        public void Init(CastContext ctx)
        {
            if(ctx.Params is not AreaParams param) return;
            _ctx = ctx;
            _onInterval = param.onEnter;
            _onExpire = param.onExpire;
            _limitTick = param.lifeTick;
            _location = new FixedVector2(transform.position);
            if (areaShape is LaserArea laser)
            {
                laser.SetMaxRange((int)(param.MaxRange * 1000));
                // 1. Start = 시전자 위치
                var start = new FixedVector2(ctx.Caster.transform.position);

                // 2. Target 방향 계산
                Vector2 targetPos = ctx.Target.transform.position;
                var dir = (targetPos - start.AsVector2).normalized;

                // 3. 거리 제한
                var distance = Vector2.Distance(start.AsVector2, targetPos);
                var height = Mathf.Min(distance, laser.MaxRange / 1000f);

                // 4. End 계산
                var endVec = start.AsVector2 + dir * height;
                var end = new FixedVector2(endVec);
                // 5. Laser 기하 확정
                laser.ResolveFromContext(start, end);
            }
            ActivateInterval();
            if (_limitTick < ActivateTick)
            {
                Expire();
            }
            else
            {
                Ticker.Instance.OnTick += TickHandler;
            }
        }

        private void TickHandler(ushort tick)
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
            switch (areaShape)
            {
                case CircleArea circle:
                {
                    var worldCenter = _location.AsVector2;
                    var radius = circle.Radius / 1000f;
                    // 물리 감지 (Player/Enemy 등 대상 레이어 필터 적용 가능)
                    var results = Physics2D.OverlapCircleAll(worldCenter, radius, LayerMask.GetMask("Foe"));
                    foreach (var col in results)
                    {
                        col.TryGetComponent<Entity>(out var entity);
                        if (entity is null) continue;

                        // OnHit FollowUp 실행
                        foreach (var followup in _onInterval)
                        {
                            if (followup.mechanism is not INewMechanism mech) continue;
                            var ctxTarget = !followup.requireRetarget ? entity.transform : null;
                            SkillCommand cmd = new(_ctx.Caster, _ctx.Mode, new FixedVector2(_ctx.Caster.position),
                                mech, followup.@params, _ctx.Damage, ctxTarget);
                            CommandCollector.Instance.EnqueueCommand(cmd);
                        }
                    }
                    break;
                }
                case IBoxLikeArea boxLike:
                {
                    var center = boxLike.CenterCoordinate.AsVector2;
                    var size = boxLike.GetBoxSize();
                    var angle = boxLike.GetRotation();

                    var results = Physics2D.OverlapBoxAll(center, size, angle, LayerMask.GetMask("Foe"));
                    foreach (var col in results)
                    {
                        col.TryGetComponent<Entity>(out var entity);
                        if (entity is null) continue;

                        foreach (var followup in _onInterval)
                        {
                            if (followup.mechanism is not INewMechanism mech) continue;
                            var ctxTarget = !followup.requireRetarget ? entity.transform : null;
                            SkillCommand cmd = new(_ctx.Caster, _ctx.Mode, new FixedVector2(_ctx.Caster.position),
                                mech, followup.@params, _ctx.Damage, ctxTarget);
                            CommandCollector.Instance.EnqueueCommand(cmd);
                        }
                    }
                    break;
                }
            }
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
            else {
                foreach (var followup in _onExpire)
                {
                    if (followup.mechanism is not INewMechanism mech) continue;
                    var ctxTarget = !followup.requireRetarget ? _ctx.Target : null;
                    SkillCommand cmd = new(_ctx.Caster, _ctx.Mode, new FixedVector2(_ctx.Caster.position),
                        mech, followup.@params, _ctx.Damage, ctxTarget);
                    CommandCollector.Instance.EnqueueCommand(cmd);
                }
            }
            Destroy(gameObject);
        }
    }
}