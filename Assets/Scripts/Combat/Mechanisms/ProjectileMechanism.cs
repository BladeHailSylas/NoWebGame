using global::NoWebGame.Combat.Effects.Runtime;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Movement.Travel;
using System;
using System.Collections.Generic;


using UnityEngine;
using Object = UnityEngine.Object;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "ProjectileMechanism")]
    [Serializable]
    public class ProjectileMechanism : NewMechanism, INewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Projectile")]
        public ProjectileEntity projectilePrefab;
        public int speed;
        public bool penetrative;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit;
        [SerializeReference] public List<MechanismData> onExpire;

        public ExitCallbackMode callbackMode;
        public TimeoutPolicy timeoutPolicy;
        [SerializeReference] public List<MechanismData> onCompleted = new();
        [SerializeReference] public List<MechanismData> onInterrupted = new();
        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not ProjectileMechanism mech)
            {
                return;
            }
            var prefab = mech.projectilePrefab;

            var instance = Object.Instantiate(
                prefab,
                ctx.Caster.position,
                Quaternion.identity
            );

            instance.Init(ctx);
        }
    }
}
