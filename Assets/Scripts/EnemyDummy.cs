// Enemy에 붙입니다. 아주 단순한 체력/넉백 처리 예시입니다.

using System;
using UnityEngine;
using ActInterfaces;
using StatsInterfaces;
using Unity.VisualScripting;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDummy : Entity, IVulnerable//, ITargetable //그냥 임시 더미, 절대 이렇게 만들면 안 됨(IVulnerable의 구현이 여기서 왜 필요함)
{
	public readonly float BasicHealth = 100000f;
	public float MaxHealth { get; private set; }
	public double Health { get; private set; }

	public float BasicArmor { get; private set; } = 4000f;
	public float Armor { get; private set; }

	public bool IsDead { get; private set; }
	//[SerializeField] Transform myTransform;
	private float _armorIncreaseRate; //방어력 버프
	private Rigidbody2D _rb;
	private SpriteRenderer _sr;

	void Awake()
	{
		_rb = GetComponent<Rigidbody2D>();
		_sr = GetComponentInChildren<SpriteRenderer>();
		MaxHealth = BasicHealth * 1.5f;
		Health = MaxHealth;
		Armor = BasicArmor * (1 + _armorIncreaseRate);
		Debug.Log($"Enemy info: Health {Health}, Armor {Armor}");
	}
	void Update()
	{
		if (Health <= 0)
		{
			Die();
		}
	}

	public void TakeDamage(int damage, int apratio, DamageType type)
	{
		TakeDamage(new DamageData(type, damage, apratio));
	}

	public void TakeDamage(DamageData data)
	{
		double nowHealth = Health;
		double damage = data.Attack * data.Value / 100.0;
		switch (data.Type)
		{
			case DamageType.MaxPercent:
				damage = Math.Round(MaxHealth * data.Value / 100);
				break;
			case DamageType.CurrentPercent:
				damage = Math.Round(Health * data.Value / 100);
				break;
			case DamageType.LostPercent:
				damage = Math.Round((MaxHealth - Health) * data.Value / 100);
				break;
		}

		if (data.Type == DamageType.Fixed) Health = Math.Max(0D, Health - damage);
		else
		{
			double armor = Armor;                      // Defender’s armor stat
			double ap = data.APRatio;          // Convert % to 0–1
			double reduction = 1;
			double effectiveArmor = armor * (1.0 - ap);
			double mitigation = 8000.0 / (8000 + effectiveArmor);
			damage = Math.Round(damage * mitigation * reduction);
			//Debug.Log($"Enemy has: 8000 / (8000 + {armor} * (1.0 - {ap})) = {mitigation}");
			Health = Math.Max(0D, Health - damage);
		}
		//Armor += (float)damage * 0.05f;
		Debug.Log($"Enemy took {damage} damage, Now Health {Health} Armor {Armor}");
	}
	System.Collections.IEnumerator Flash()
	{
		var original = _sr.color;
		_sr.color = Color.white;
		yield return new WaitForSeconds(0.06f);
		_sr.color = original;
	}
	public void Die()
	{
		// TODO: 사망 연출
		//Debug.Log("Now that's a LOTTA damage");
		IsDead = true;
		Destroy(gameObject);
	}
}
