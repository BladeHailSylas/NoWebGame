using Systems.Data;
using UnityEngine;

namespace Systems.Stacks.Definition
{
    [CreateAssetMenu(menuName = "Stacks/CCDefinition")]
    public class CCStackDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}