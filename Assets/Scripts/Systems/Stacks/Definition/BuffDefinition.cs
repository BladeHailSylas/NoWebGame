using Systems.Data;
using UnityEngine;

namespace Systems.Stacks.Definition
{
    [CreateAssetMenu(menuName = "Stacks/BuffDefinition")]
    public class BuffDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}