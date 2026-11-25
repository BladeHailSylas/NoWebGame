using UnityEngine;

namespace NoWebGame.Combat.Targeting.Anchors
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Anchor", "Assembly-CSharp", "SkillAnchor")]
    public sealed class SkillAnchor : MonoBehaviour
    {
        public int castId;              // 고유 식별
        public Transform owner;          // caster
        public long startTick;         // 생성 시점
        public bool active;              // 유효 여부
    }

}