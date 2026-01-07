using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(menuName = "Skills/Mechanisms/Area")]
    public class AreaMechanism : ObjectGeneratingMechanism
    {
        public override void Execute(CastContext ctx)
        {
            if (ctx.Params is not AreaParams param)
            {
                Debug.LogError("[AreaMechanism] Invalid parameter type.");
                return;
            }
            
            var centerPos = ctx.Target?.position ?? ctx.Caster.position;
            var go = Instantiate(param.areaPrefab, centerPos, Quaternion.identity);
            var dir = ctx.Target is not null
                ? (ctx.Target.position - ctx.Caster.position).normalized
                : ctx.Caster.right;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
            if (!go.TryGetComponent<AreaEntity>(out var entity)) return;
            entity.Init(ctx, param);
        }
    }
    
    [Serializable]
    public class AreaParams : INewParams
    {
        [Header("Time")]
        [SerializeField] private short cooldownTicks;
        public ushort lifeTick;

        [Header("Settings")] 
        public LayerMask mask;
        public AreaEntity areaPrefab;
        public List<MechanismRef> onAreaEnter;
        public List<MechanismRef> onAreaExpire;
        public short CooldownTicks => cooldownTicks;
    }
}