using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Stacks/BaseDefinition")]
public class StackDefinition : ScriptableObject
{
    [Header("Metadata")]
    public int Id;
    public string DisplayName;
    public ushort DurationTicks = 100;
    public int MaxStacks = 1;
    public int Priority = 0;
    public bool IsDebuff;
    public GameObject VisualPrefab;

    //[Header("Stat Modifiers")]
    //public List<StatModifier> Modifiers;

}