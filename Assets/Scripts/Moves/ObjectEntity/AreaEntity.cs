using System.Collections.Generic;
using JetBrains.Annotations;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Ticker;
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
        private DamageData _damage;
        private List<MechanismRef> _onInterval;
        private List<MechanismRef> _onExpire;
        private FixedVector2 _location;
        private Transform _originalCaster;
        private CastContext _ctx;
        public void Init(CastContext ctx, AreaParams param)
        {
            _ctx = ctx;
            _damage = ctx.Damage;
            _onInterval = param.onAreaEnter;
            _onExpire = param.onAreaExpire;
            _limitTick = param.lifeTick;
            _location = new FixedVector2(transform.position);
            _originalCaster = ctx.Caster;
            ActivateInterval();
            if (_limitTick < ActivateTick)
            {
                Debug.Log("lifetime is under 0.25s; instant activation");
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
                case BoxArea box:
                {
                    var center = _location.AsVector2;
                    Vector2 size = new(box.Width / 1000f, box.Height / 1000f);

                    var results = Physics2D.OverlapBoxAll(center, size, transform.eulerAngles.z, LayerMask.GetMask("Foe"));
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
            //Debug.Log($"{intervalActivated} times of activation");
            foreach (var followup in _onExpire)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                var ctxTarget = !followup.requireRetarget ? _ctx.Target : null;
                SkillCommand cmd = new(_ctx.Caster, _ctx.Mode, new FixedVector2(_ctx.Caster.position),
                    mech, followup.@params, _ctx.Damage, ctxTarget);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
            Destroy(gameObject);
        }
    }
}