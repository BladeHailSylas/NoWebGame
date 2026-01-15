using Systems.Stacks.ExclusiveGroups;
using UnityEngine;

namespace Systems.Stacks.Definition
{
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