using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using System.Collections.Generic;



using UnityEngine;
using UnityEngine.Serialization;

namespace NoWebGame.Characters
{
	[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Characters", "Assembly-CSharp", "SkillBinding")]
    [System.Serializable]
	public struct SkillBinding
	{
		public SkillSlot slot;
		// 기존 에셋 역직렬화용으로만 유지합니다. 루트 Mode는 skillData.mechanismData.mode를 사용합니다.
		[HideInInspector] public TargetMode mode;
		public SkillData skillData;
	}

	[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Characters", "Assembly-CSharp", "CharacterSpec")]
[CreateAssetMenu(menuName = "Game/Characters/Spec")]
	public class CharacterSpec : ScriptableObject
	{
		public string displayName;
		public int baseHp, baseHpGen, baseMana, baseManaGen, baseAttack, baseDefense, baseSpeed;
		public List<VariableDefinition> CharacterVariables;
		public SkillBinding attack, skill1, skill2, ultimate;
	}
}
