using SkillInterfaces;
using UnityEngine;

[System.Serializable]
public struct SkillBinding
{
	public SkillSlot slot;
	public ScriptableObject mechanism;
	[SerializeReference] public ISkillParam param;
}

[CreateAssetMenu(menuName = "Game/Characters/Spec")]
public class CharacterSpec : ScriptableObject
{
	public string displayName;
	public int baseHp, baseHpGen, baseMana, baseManaGen, baseAttack, baseDefense, baseSpeed;
	public SkillBinding attack, skill1, skill2, ultimate;
}