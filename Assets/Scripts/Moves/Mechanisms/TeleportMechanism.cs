using System;
using System.Collections.Generic;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "TeleportMechanism", menuName = "Skills/Mechanisms/Teleport")]
    public class TeleportMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            //ITeleportative
            if (ctx.Params is not TeleportParams param) return;
            if(!ctx.Caster.TryGetComponent<ITeleportative>(out var tp)) return;
            var contract = new TeleportContract(ctx);
            tp.AddTeleportContract(contract);
        }
    }

    [Serializable]
    public class TeleportParams : INewParams
    {
        public short CooldownTicks { get; private set; }
        // Range limits for this mechanism (world units).
        public float minRange;
        public float maxRange;
        public bool ignoreEnemy;
        public List<MechanismRef> onArrival;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
    }
}
