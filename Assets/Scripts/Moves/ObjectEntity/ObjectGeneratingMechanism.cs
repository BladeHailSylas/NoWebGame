using Systems.Data;
using UnityEngine;

namespace Moves.ObjectEntity
{
    /// <summary>
    /// Base class for any mechanism that needs to spawn objects in the world deterministically.
    /// </summary>
    public abstract class ObjectGeneratingMechanism : ScriptableObject, INewMechanism
    {
        /// <summary>
        /// Creates a new GameObject at the given position.
        /// Duration-based expiration will be added later.
        /// </summary>
        protected GameObject GenerateObject(string name, Vector3 position, ushort durationTicks)
        {
            var obj = new GameObject(name)
            {
                transform =
                {
                    position = position
                }
            };
            return obj;
        }
        protected GameObject GenerateObject(string name, Vector2 position)
        {
            return GenerateObject(name, position, 0);
        }

        protected GameObject GenerateObject(ObjectPrefab prefab, FixedVector2 position)
        {
            var obj = new GameObject(prefab.name)
            {
                transform =
                {
                    position = position.AsVector2
                }
            };
            return obj;
        }
        /// <summary>
        /// All mechanisms must implement this — defines their activation behavior.
        /// </summary>
        public abstract void Execute(CastContext ctx);
    }
}