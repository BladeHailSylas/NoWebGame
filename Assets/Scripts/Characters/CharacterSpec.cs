using System.Collections.Generic;
using Moves;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;
using UnityEngine.Serialization;

namespace Characters
{
	[System.Serializable]
	public struct SkillBinding
	{
		public SkillSlot slot;
		// 기존 에셋 역직렬화용으로만 유지합니다. 루트 Mode는 skillData.mechanismData.mode를 사용합니다.
		[HideInInspector] public TargetMode mode;
		public SkillData skillData;
	}

	[CreateAssetMenu(menuName = "Game/Characters/Spec")]
	public class CharacterSpec : ScriptableObject
	{
		public string displayName;
		public int baseHp, baseHpGen, baseMana, baseManaGen, baseAttack, baseDefense, baseSpeed;
		public List<VariableDefinition> CharacterVariables;
		public SkillBinding attack, skill1, skill2, ultimate;
	}
}
