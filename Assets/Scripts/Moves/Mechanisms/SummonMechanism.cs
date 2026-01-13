using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "SummonMechanism", menuName = "Skills/Mechanisms/Summon")]
    public class SummonMechanism : ObjectGeneratingMechanism
    {
        public override void Execute(CastContext ctx)
        {
            if (ctx.Params is not SummonParams param)
            {
                Debug.LogError("[AreaMechanism] Invalid parameter type.");
                return;
            }
            
            var centerPos = ctx.Target?.position ?? ctx.Caster.position;
            var go = Instantiate(param.summonPrefab, centerPos, Quaternion.identity);
            var dir = ctx.Target is not null
                ? (ctx.Target.position - ctx.Caster.position).normalized
                : ctx.Caster.right;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
            if (!go.TryGetComponent<AreaEntity>(out var entity)) return;
            entity.Init(ctx);
        }
    }
    
    [Serializable]
    public class SummonParams : NewParams
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public SummonEntity summonPrefab;
        public List<MechanismRef> onEnter;
        public List<MechanismRef> onExpire;
    }
}