using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public interface IStack
{
    string Name { get; }
    int MaxCount { get; }
    ushort ExpireAt { get; }
}

public readonly struct Stack : IStack
{
    public string Name { get; }
    public int MaxCount { get; }
    public ushort AppliedAt { get; }
    public ushort ContinueBy { get; }
    public ushort ExpireAt { get; }
    public string ApplierName { get; }

    public Stack(string name, int maxCount, ushort appliedAt, ushort continueBy, string applierName = "The World")
    {
        Name = name;
        ApplierName = applierName;
        MaxCount = maxCount;
        AppliedAt = appliedAt;
        ContinueBy = continueBy;
        if (ContinueBy == 65535) ExpireAt = 65535;
        else ExpireAt = (ushort)(AppliedAt + ContinueBy);
    }
}

public static class StacksRegistry
{
    public static List<Stack> StacksDefined { get; private set; } = new();
    static StacksRegistry()
    {
        StacksDefined.Add(new Stack("Dummy", Int32.MaxValue, 0, 65535));
    }
}