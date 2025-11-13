using EffectInterfaces;
using UnityEngine;

[CreateAssetMenu(menuName = "Stacks/CCDefinition")]
public class CCStackDefinition : StackDefinition
{
    public EffectType Type;
    public byte Value;
}