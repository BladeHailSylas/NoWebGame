using UnityEngine;

namespace Systems.Anchor
{
    public sealed class SkillAnchor : MonoBehaviour
    {
        public int castId;              // 고유 식별
        public Transform owner;          // caster
        public ushort startTick;         // 생성 시점
        public bool active;              // 유효 여부
    }

}