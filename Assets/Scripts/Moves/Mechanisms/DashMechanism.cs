using System;
using System.Collections.Generic;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "DashMechanism", menuName = "Skills/Mechanisms/Dash")]
    public class DashMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not DashParams param) return;
            if(!ctx.Caster.TryGetComponent<IDashable>(out var dash)) return;
            var contract = new DashContract(
                ctx, param.durationTicks, param.speed, param.preventActivation,
                param.penetrative, param.onHit, param.onExpire,
                param.expireWhenUnexpected
            );
            dash.AddDashContract(contract);
        }
    }

    [Serializable]
    public class DashParams : INewParams
    {
        public short CooldownTicks { get; private set; }
        public ushort durationTicks;
        public int speed;
        public bool preventActivation;
        public bool penetrative;
        public bool expireWhenUnexpected;
        // Range limits for this mechanism (world units).
        public float minRange;
        public float maxRange;
        public List<MechanismRef> onHit;
        public List<MechanismRef> onExpire;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
    }
}
