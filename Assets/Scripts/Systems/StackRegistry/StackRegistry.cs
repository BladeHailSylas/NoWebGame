using System.Collections.Generic;
using UnityEngine;

public sealed class StackRegistry
{
    public static StackRegistry Instance { get; private set; }
    public List<StackDefinition> Stacks { get; private set; } = new();

    public StackRegistry(List<StackDefinition> stacks)
    {
        Instance = this;
        Stacks = stacks;
    }

    public void AddToStackList(StackDefinition stack)
    {
        Stacks.Add(stack);
    }
}