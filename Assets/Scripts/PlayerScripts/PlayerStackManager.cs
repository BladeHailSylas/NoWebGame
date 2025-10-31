using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PlayerStackManager
{
    public List<(Stack, ushort)> CurrentStacks = new();
    public void ApplyStack(Stack stack)
    {
        CurrentStacks.Add((stack, 1));
        //Debug.Log($"Stack {stack.Name} has added");
    }
    
    public void TickHandler(ushort tick)
    {
        CurrentStacks[0] = (CurrentStacks[0].Item1, (ushort)(CurrentStacks[0].Item2 - 1));
    }
}