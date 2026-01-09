using Systems.Data;
using UnityEngine;

namespace Systems.Stacks.Definition
{
    [CreateAssetMenu(menuName = "Stacks/CCDefinition")]
    public class CCDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}