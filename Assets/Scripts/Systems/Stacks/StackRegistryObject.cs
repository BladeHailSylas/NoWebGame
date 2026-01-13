using System.Collections.Generic;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Systems.Stacks
{
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