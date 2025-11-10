using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PlayerStackManager
{
    public Dictionary<StackProperty, StackStatus> CurrentStacks { get; private set; } = new();
    public void ApplyStack(StackProperty stackProperty, int amount, ushort tick)
    {
        ushort endAt = Math.Max((ushort)(tick + stackProperty.def.defaultDuration), (ushort)65535);
        var total = Math.Min(amount, stackProperty.def.maxStacks);
        if (CurrentStacks.ContainsKey(stackProperty))
        {
            total = Math.Min(total + CurrentStacks[stackProperty].amount, stackProperty.def.maxStacks);
            CurrentStacks[stackProperty] = new StackStatus(total, tick, endAt);
        }
        else
        {
            CurrentStacks.Add(stackProperty, new StackStatus(total, tick, endAt));
        }
        //Debug.Log($"Currently {stackProperty.def.displayName} is {CurrentStacks[stackProperty].amount} expires {CurrentStacks[stackProperty].expireAt}");
    }
}