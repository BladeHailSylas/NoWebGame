using global::NoWebGame.Moves;

using UnityEngine;

namespace NoWebGame.Combat.Stacks.Definitions
{
    /// <summary>
    /// TriggerableDefinition은 StackDefinition의 하위 개념으로, 특정 조건에서 자동으로 발동되는 스택을 정의합니다.
    /// TriggerableDefinition은 threshold와 effect를 포함하며, threshold는 발동 조건을 나타내고, effect는 발동 시 실행될 Mechanism을 정의합니다.
    /// threshold: 발동 조건을 나타내는 정수 값입니다. 이 값은 스택의 상태나 다른 게임 로직에 따라 결정될 수 있습니다.
    /// effect: 발동 시 실행될 Mechanism을 정의하는 MechanismData 객체입니다. 이 Mechanism은 TriggerableDefinition이 발동될 때 실행됩니다.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "TriggerableDefinition")]
[CreateAssetMenu(menuName = "Stacks/TriggerableDefinition")]
    [System.Serializable]
    public class TriggerableDefinition : StackDefinition
    {
        public int threshold;
        public MechanismData effect;
    }
}