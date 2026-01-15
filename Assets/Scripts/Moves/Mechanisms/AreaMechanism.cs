using System;
using System.Collections.Generic;
using Moves.ObjectEntity;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(menuName = "Skills/Mechanisms/Area")]
    public class AreaMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not AreaParams param)
            {
                return;
            }
            var centerPos = ctx.Target?.position ?? ctx.Caster.position;
            var go = Instantiate(param.areaPrefab, centerPos, Quaternion.identity);
            var dir = ctx.Target is not null
                ? (ctx.Target.position - ctx.Caster.position).normalized
                : ctx.Caster.right;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
            if (!go.TryGetComponent<AreaEntity>(out var entity)) return;
            entity.Init(ctx);
        }
    }
    
    [Serializable]
    public class AreaParams : NewParams
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")] 
        public AreaEntity areaPrefab;
        public List<MechanismRef> onEnter;
        public List<MechanismRef> onExpire;
    }
}
