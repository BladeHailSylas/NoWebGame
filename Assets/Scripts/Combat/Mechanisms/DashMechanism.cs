using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Movement;
using global::NoWebGame.Movement.Travel;
using System;
using System.Collections.Generic;



using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "DashMechanism")]
    [Serializable]
    public class DashMechanism : NewMechanism, INewMechanism
    {
        public ushort durationTicks;
        public int speed;
        public bool preventActivation;
        public bool penetrative;
        public bool expireWhenUnexpected;
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;
        public ExitCallbackMode callbackMode;
        public TimeoutPolicy timeoutPolicy;
        [SerializeReference] public List<MechanismData> onCompleted = new();
        [SerializeReference] public List<MechanismData> onInterrupted = new();
        protected override void ExecuteCore(CastContext ctx)
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
