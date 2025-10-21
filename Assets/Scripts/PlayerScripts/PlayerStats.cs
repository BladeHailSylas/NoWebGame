using StatsInterfaces;
using System.Collections.Generic;
using System;
using UnityEngine;
public sealed class PlayerStats : MonoBehaviour // 플레이어 스탯 관리, 다른 곳에서는 참조만
{
	[SerializeReference] readonly CharacterSpec _spec;
	public int BaseHealth { get; private set; }
	public int MaxHealth { get; private set; }
	public int Health { get; private set; }
	public int Shield { get; private set; }
	public int SpecialShield { get; private set; }
	public int BaseArmor { get; private set; }
	public int Armor { get; private set; }
	public int BaseHealthRegen { get; private set; }
	public int HealthRegen { get; private set; }
	public List<int> DamageReduction { get; private set; } = new();
	public int BaseAttackDamage { get; private set; } = 10;
	public int AttackDamage { get; private set; } = 12;
	public List<int> ArmorPenetration { get; private set; } = new();
	public int BaseMana { get; private set; }
	public int MaxMana { get; private set; }
	public int Mana { get; private set; }
	public int BaseManaRegen { get; private set; }
	public int ManaRegen { get; private set; }
	public int BaseSpeed { get; private set; } = 8;
	public int Speed { get; private set; } = 8;
	public int JumpTime { get; private set; }
	public bool OnGround { get; private set; }
	public bool IsDead { get; private set; }
	private void Awake()
	{
		/*BaseHealth = _spec.baseHp;
		BaseHealthRegen = _spec.baseHpGen;
		BaseArmor = _spec.baseDefense;
		BaseAttackDamage = _spec.baseAttack;
		BaseMana = _spec.baseMana;
		BaseManaRegen = _spec.baseManaGen;
		BaseSpeed = _spec.baseSpeed;*/
		BaseSpeed = 8000;
	}
	public void ReduceStat(ReduceType stat, int amount, int apRatio = 0, DamageType type = DamageType.Normal)
	{
		if (stat == ReduceType.Mana) // 1(Mana)이면 마나, 0(Health)이면 체력
		{
			Mana = (int)Math.Max(0f, Mana - amount);
		}
		else
		{
			GetDamage(amount, apRatio, type);
		}
		if (Health <= 0f) IsDead = true;
	}
	void GetDamage(int damage, int apRatio = 0, DamageType type = DamageType.Normal) //비례 피해를 여기서 계산해야 되나 << 그럴 거 같지 않음
	{
		if (IsDead || damage <= 0f) return;
		switch (type) 
		{ 
			case DamageType.CurrentPercent:
				damage = Health * damage / 100;
				break;
			case DamageType.LostPercent:
				damage = (MaxHealth - Health) * damage / 100;
				break;
			case DamageType.MaxPercent:
				damage = MaxHealth * damage / 100;
				break;
			default:
				break;
		}
		if (type != DamageType.Fixed) damage *= (int)DamageReductionCalc(Armor, apRatio, TotalDamageReduction());
		if (SpecialShield > damage)
		{
			SpecialShield -= damage;
		}
		else if (SpecialShield + Shield > damage)
		{
			Shield -= SpecialShield - damage;
		}
		else
		{
			Health -= Shield + SpecialShield - damage;
		}
		if (Health <= 0f)
		{
			IsDead = true;
		}
	}
	double DamageReductionCalc(int armor, int apRatio = 0, double damageRatio = 1) //Player가 피해를 받는 경우
		=> (double)(80 / (80 + armor * (1 - apRatio))) * damageRatio;
	public double TotalArmorPenetration() //AP를 반환하는 거면 1 - totalAP가 맞는데 그럼 계산이 귀찮아짐, 명칭을 바꾸는 것이 맞지 않나
										//어쩌면 괜찮을지도 모르겠다, 어차피 AP 계산식은 1 - (80 / (80 + Armor * (1 - TotalAP)))로 이미 정해져 있으니까
										//오히려 그 공식을 바꾸려 들었다가 수식이 달라 혼란이 올 가능성이 있음, 이름도 ArmorPenetration에서 ArmorRatio로 바꿔야 됨
	{
		float totalAP = 1f;
		foreach (var ap in ArmorPenetration)
		{
			totalAP *= (1 - ap / 100); // 단일 AP 비율이 100%를 넘으면 방어력이 마이너스라 피해 배율이 너무 커지므로 그런 일이 없어야 함
		}
		return 1 - totalAP;
	}
	public double TotalDamageReduction()
	{
		float totalDr = 1f;
		foreach (var dr in DamageReduction)
		{
			totalDr *= (1 - dr / 100);
		}
		return Mathf.Max(0.15f, totalDr); //공격자 우선(하게 두되 대안을 주어라) -> 대미지가 들어가게 두되 다른 생존 수단(체력 회복, 보호막 등)으로 원콤이 안 나게 하라
											//왜 하한을 두나요? 안 그러면 맞는데 피가 닳는 대신 회복하는 망겜이 되어버림
	}
}