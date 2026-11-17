using System;
using UnityEngine;

namespace Moves
{
    // Unity가 ScriptableObject 에셋을 저장/재로드하려면 파일명과 클래스명이 일치해야 합니다.
    // 기존 Generals.cs의 GUID를 이 파일로 옮겨 기존 SkillData 스크립트 참조를 유지합니다.
    [Serializable]
    [CreateAssetMenu(menuName = "Skills/SkillData", fileName = "SkillData")]
    public class SkillData : ScriptableObject
    {
        [SerializeReference] public MechanismData mechanismData;
    }
}
