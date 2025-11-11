using System.Collections.Generic;
using UnityEngine;

public class StackRegistryObject : MonoBehaviour
{
    private StackRegistry _stack;
    public List<StackDefinition> stacks;
    void OnEnable()
    {
        _stack = new StackRegistry(stacks);
    }
}