using System.Collections.Generic;
using Systems.Stacks.Definition;

namespace Systems.Stacks.Instances
{
    public static class StackStorage
    {
        public static Dictionary<string, StackDefinition> Storage { get; internal set; }
    }
}