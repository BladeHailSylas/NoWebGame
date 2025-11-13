using EffectInterfaces;
using UnityEngine;

[CreateAssetMenu(menuName = "Stacks/BuffDefinition")]
public class BuffStackDefinition : StackDefinition
{
    public EffectType Type;
    public byte Value;
}