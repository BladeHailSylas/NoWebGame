using System.Collections.Generic;
using Systems.Stacks.Definition;

namespace Systems.Stacks
{
    public sealed class StackRegistry
    {
        public static StackRegistry Instance { get; private set; }
        private List<StackDefinition> Stacks;
        public Dictionary<string, StackDefinition> StackStorage { get; private set; }

        public StackRegistry(List<StackDefinition> stacks)
        {
            Instance = this;
            Stacks = stacks;
            StackStorage = new Dictionary<string, StackDefinition>();
            foreach (var stack in Stacks)
            {
                StackStorage.Add(stack.displayName, stack);
            }
        }

        public void AddToStackList(StackDefinition stack)
        {
            Stacks.Add(stack);
        }
    }
}