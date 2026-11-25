using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Movement;
using System.Collections.Generic;



using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "TeleportMechanism")]
    [System.Serializable]
    public class TeleportMechanism : NewMechanism, INewMechanism
    {
        [Header("Settings")]
        public bool ignoreEnemy;
        [SerializeReference] public List<MechanismData> onArrival;

        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not TeleportMechanism mech) return;
            if (!ctx.Caster.TryGetComponent<ITeleportative>(out var tp)) return;

            // The runtime contract now resolves all teleport options from the mechanism itself.
            var contract = new TeleportContract(ctx);
            tp.AddTeleportContract(contract);
        }
    }
}
