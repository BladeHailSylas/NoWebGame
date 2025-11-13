using UnityEngine;

[CreateAssetMenu(menuName = "Stacks/VariableDefinition")]
public class VariableDefinition : StackDefinition
{
    public bool isPeriodic;
    public bool isExclusive;
    public ushort periodTick; // Don't need to change if not periodic
    public VariableDefinition exclusiveVariable;
    public byte exclusivePriority;
}