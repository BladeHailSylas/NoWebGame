using global::NoWebGame.Combat.Stacks.Definitions;
using System.Collections.Generic;


namespace NoWebGame.Combat.Stacks.Registry
{
    public static class StackStorage
    {
        public static Dictionary<string, StackDefinition> Storage { get; internal set; }
    }
}