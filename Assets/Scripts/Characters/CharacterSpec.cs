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
		public TargetMode mode;
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