using global::NoWebGame.Systems.Data;

using UnityEngine;

namespace NoWebGame.Combat.Stacks.Definitions
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "CCDefinition")]
[CreateAssetMenu(menuName = "Stacks/CCDefinition")]
    public class CCDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}