using System;
using EffectInterfaces;
using UnityEngine;

[CreateAssetMenu(menuName = "Stacks/BaseDefinition")]
public class StackDefinition : ScriptableObject
{
    [Header("Metadata")]
    public int id;
    public string displayName;
    public int maxStacks = 1000000;
    public ushort defaultDuration;
    public GameObject visualPrefab;

    //[Header("Stat Modifiers")]
    //public List<StatModifier> Modifiers;
    public Action<Entity> OnApply;
    public Action<Entity> OnRemove;
}

[CreateAssetMenu(menuName = "Stacks/BuffDefinition")]
public class BuffDefinition : StackDefinition
{
    
}