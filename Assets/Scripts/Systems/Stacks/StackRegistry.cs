using System;
using System.Collections.Generic;
using System.IO;
using Systems.Stacks.Definition;
using Systems.Stacks.Instances;

namespace Systems.Stacks
{
    public sealed class StackRegistry
    {
        public static StackRegistry Instance { get; private set; }
        private List<StackDefinition> Stacks;

        public StackRegistry(List<StackDefinition> stacks)
        {
            //Let the maxStack of Expirable Periodic Variables be 1
            Instance = this;
            Stacks = stacks;
            StackStorage.Storage = new Dictionary<string, StackDefinition>();
            foreach (var stack in Stacks)
            {
                if (stack is VariableDefinition va)
                {
                    switch (va.periodicType)
                    {
                        case PeriodicType.Recharging when va.maxStacks != 1: // Recharging => maxStacks가 1이어야 함
                            throw new InvalidDataException($"{va.displayName} must be recharging, but its maxStacks is not");
                        case PeriodicType.Accumulating when va.maxStacks <= 1: // Accumulating => maxStacks가 2 이상이어야 함
                            throw new InvalidDataException($"{va.displayName} must be accumulating, but its maxStacks is not");
                        case PeriodicType.Accumulating when va.duration is not 65535: //Accumulating인데 Expirable일 경우
                            throw new InvalidDataException($"{va.displayName} must be permanent since it's periodic-accumulating, but its duration is not");
                    }
                }
                StackStorage.Storage.Add(stack.displayName, stack);
            }
        }

        public void AddToStackList(StackDefinition stack)
        {
            Stacks.Add(stack);
        }
    }
}