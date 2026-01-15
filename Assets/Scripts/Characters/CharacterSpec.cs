using System.Collections.Generic;
using Moves;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Characters
{
	[System.Serializable]
	public struct SkillBinding
	{
		public SkillSlot slot;
		public TargetMode mode;
		public ScriptableObject mechanism;
		[SerializeReference] public INewParams @params;
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