using System.Collections.Generic;
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
        private ushort limitTick;
        private ushort lifeTick;
        private ushort _tickElapsed;
        private DamageData damage;
        private List<MechanismRef> onInterval;
        private List<MechanismRef> onExpire;
        private byte activateTick = 15;
        private FixedVector2 location;
        private Transform originalCaster;
        public void Init(DamageData dmg, List<MechanismRef> interval, List<MechanismRef> expire, Transform caster, ushort life = 0)
        {
            damage = dmg;
            onInterval = interval;
            onExpire = expire;
            limitTick = life;
            location = new FixedVector2(transform.position);
            originalCaster = caster;
            ActivateInterval();
            if (limitTick < activateTick)
            {
                Debug.Log("lifetime is under 0.25s; instant activation");
                Expire();
            }
            else
            {
                Ticker.Instance.OnTick += TickHandler;
            }
        }
        void OnDisable()
        {
            Expire();
        }
        void TickHandler(ushort tick)
        {
            lifeTick++; _tickElapsed++;
            if (lifeTick >= limitTick)
            {
                Expire();
            }
            else if(_tickElapsed >= activateTick)
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
                    var worldCenter = location.AsVector2;
                    var radius = circle.Radius / 1000f;
                    // 물리 감지 (Player/Enemy 등 대상 레이어 필터 적용 가능)
                    var results = Physics2D.OverlapCircleAll(worldCenter, radius, LayerMask.GetMask("Foe"));
                    foreach (var col in results)
                    {
                        col.TryGetComponent<Entity>(out var entity);
                        if (entity is null) continue;

                        // OnHit FollowUp 실행
                        foreach (var followup in onInterval)
                        {
                            if (followup.mechanism is not INewMechanism mech) continue;
                            SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                                location, mech, followup.@params, damage, entity.transform);
                            CommandCollector.Instance.EnqueueCommand(cmd);
                        }
                    }
                    break;
                }
                case BoxArea box:
                {
                    var center = location.AsVector2;
                    Vector2 size = new(box.Width / 1000f, box.Height / 1000f);

                    var results = Physics2D.OverlapBoxAll(center, size, transform.eulerAngles.z, LayerMask.GetMask("Foe"));
                    foreach (var col in results)
                    {
                        col.TryGetComponent<Entity>(out var entity);
                        if (entity is null) continue;

                        foreach (var followup in onInterval)
                        {
                            if (followup.mechanism is not INewMechanism mech) continue;

                            SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                                location, mech, followup.@params, damage, entity.transform);
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
            foreach (var followup in onExpire)
            {
                if (followup.mechanism is not INewMechanism mech) continue;

                SkillCommand cmd = new(originalCaster, TargetMode.TowardsEntity,
                    location, mech, followup.@params, damage);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
            Destroy(gameObject);
        }
    }
}