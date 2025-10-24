using StatsInterfaces;
using UnityEngine;
using System;
[Obsolete]
public class PlayerStatsBridge : MonoBehaviour, IStatProvider
{
	//필요가 없다?
	[SerializeField] PlayerStats stats;
	[SerializeField] PlayerEffects effects;
	public float GetBool(StatBool sb)
	{
		return sb switch
		{
			StatBool.OnGround => stats.OnGround ? 1f : 0f,
			StatBool.IsDead => stats.IsDead ? 1f : 0f,
			_ => 0f,
		};
	}
	public double GetArmorRatio() //플레이어가 피해를 가하는 경우에만 쓰임
	{
		return stats.TotalArmorPenetration();
	}
	public void ReduceStat(ReduceType stat, int damage, int armorRatio = 1, DamageType type = DamageType.Normal) //The only way to access stats
	{
		stats.ReduceStat(stat, damage, armorRatio, type);
	}
}
/*public sealed class ArmorPenPercentMod : IStatModifier
{
	public readonly float Percent;  // 0~100
	public ArmorPenPercentMod(float percent) { Percent = Mathf.Clamp(percent, 0, 100); }
	public void Apply(PlayerStats s) => s.AddArmorPen(Percent);
	public void Remove(PlayerStats s) => s.RemoveArmorPen(Percent);
}*/