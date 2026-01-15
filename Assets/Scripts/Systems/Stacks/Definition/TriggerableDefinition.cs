using Moves;
using UnityEngine;

namespace Systems.Stacks.Definition
{
    [CreateAssetMenu(menuName = "Stacks/TriggerableDefinition")]
    public class TriggerableDefinition : StackDefinition
    {
        public int threshold;
        public MechanismRef effect;
    }
}