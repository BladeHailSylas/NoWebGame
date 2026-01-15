using System;
using Systems.Data;
using UnityEngine;

namespace Moves.ObjectEntity
{
    /// <summary>
    /// Base class for any mechanism that needs to spawn objects in the world deterministically.
    /// </summary>
    [Obsolete("ObjectGeneratingMechanism is deprecated due to the prefab replacement.", true)]
    public abstract class ObjectGeneratingMechanism : ScriptableObject, INewMechanism
    {
        /// <summary>
        /// All mechanisms must implement this — defines their activation behavior.
        /// </summary>
        public abstract void Execute(CastContext ctx);
    }
}