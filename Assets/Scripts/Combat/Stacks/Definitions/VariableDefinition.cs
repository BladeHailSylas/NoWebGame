
using UnityEngine;

namespace NoWebGame.Combat.Stacks.Definitions
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "VariableDefinition")]
[CreateAssetMenu(menuName = "Stacks/VariableDefinition")]
    public class VariableDefinition : StackDefinition
    {
        public bool isPeriodic;
        public PeriodicType periodicType;
        public bool isExclusive;
        public ushort periodTick; // Don't need to change if not periodic
        public ExclusiveGroup[] exclusiveGroup;
        public byte exclusivePriority;

    }
}