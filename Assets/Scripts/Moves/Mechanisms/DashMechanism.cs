using System;
using System.Collections.Generic;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class DashMechanism : NewMechanism
    {
        public ushort durationTicks;
        public int speed;
        public bool preventActivation;
        public bool penetrative;
        public bool expireWhenUnexpected;
        public List<MechanismData> onHit;
        public List<MechanismData> onExpire;
        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not DashMechanism mech) return;
            if(!ctx.Caster.TryGetComponent<IDashable>(out var dash)) return;
            var contract = new DashContract(
                ctx, mech.durationTicks, mech.speed, mech.preventActivation,
                mech.penetrative, mech.onHit, mech.onExpire,
                mech.expireWhenUnexpected
            );
            dash.AddDashContract(contract);
        }
    }
}
