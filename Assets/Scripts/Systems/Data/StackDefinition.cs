using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public interface IStack
{
    string Name { get; }
    int MaxCount { get; }
    ushort DurationTick { get; }
    [CanBeNull] Action<int> OnApplied { get; }
    [CanBeNull] Action<int> OnRemoved { get; }
}

public readonly struct Stack : IStack
{
    public string Name { get; }
    public int MaxCount { get; }
    public ushort DurationTick { get; }
    public Action<int> OnApplied { get; }
    public Action<int> OnRemoved { get; }

    public Stack(string name, int maxCount, ushort durationTick, Action<int> onApplied, Action<int> onRemoved)
    {
        Name = name;
        MaxCount = maxCount;
        DurationTick = durationTick;
        OnApplied = onApplied;
        OnRemoved = onRemoved;
    }
}

public static class StacksContainer
{
    public static List<Stack> StacksDefined { get; private set; } = new();
    static StacksContainer()
    {
        StacksDefined.Add(new Stack("Hello there", Int32.MaxValue, 65535,
            _ => { Debug.Log($"Stack applied");}, _ => {Debug.Log("Stack removed");}));
    }
}