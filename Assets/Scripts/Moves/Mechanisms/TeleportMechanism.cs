using System.Collections.Generic;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class TeleportMechanism : NewMechanism
    {
        [Header("Settings")]
        public bool ignoreEnemy;
        public List<MechanismData> onArrival;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not TeleportMechanism mech) return;
            if (!ctx.Caster.TryGetComponent<ITeleportative>(out var tp)) return;

            // The runtime contract now resolves all teleport options from the mechanism itself.
            var contract = new TeleportContract(ctx);
            tp.AddTeleportContract(contract);
        }
    }
}
