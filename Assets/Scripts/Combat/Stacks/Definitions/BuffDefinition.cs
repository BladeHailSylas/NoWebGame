using global::NoWebGame.Systems.Data;

using UnityEngine;

namespace NoWebGame.Combat.Stacks.Definitions
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "BuffDefinition")]
[CreateAssetMenu(menuName = "Stacks/BuffDefinition")]
    public class BuffDefinition : StackDefinition
    {
        public EffectType Type;
        public byte Value;
    }
}