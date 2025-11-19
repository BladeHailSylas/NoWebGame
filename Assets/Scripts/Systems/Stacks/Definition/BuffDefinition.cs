using Systems.Data;
using UnityEngine;

namespace Systems.Stacks.Definition
{
    [CreateAssetMenu(menuName = "Stacks/BuffDefinition")]
    public class BuffStackDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}