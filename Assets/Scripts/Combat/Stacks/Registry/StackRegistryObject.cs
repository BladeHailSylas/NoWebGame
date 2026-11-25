using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Combat.Stacks.Registry;
using System.Collections.Generic;

using UnityEngine;

namespace NoWebGame.Combat.Stacks.Registry
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks", "Assembly-CSharp", "StackRegistryObject")]
    public class StackRegistryObject : MonoBehaviour
    {
        private StackRegistry _stack;
        public List<StackDefinition> stacks;

        private void OnEnable()
        {
            _stack = new StackRegistry(stacks);
        }
    }
}