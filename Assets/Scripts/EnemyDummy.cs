// Enemy에 붙입니다. 아주 단순한 체력/넉백 처리 예시입니다.

using System;
using UnityEngine;
using ActInterfaces;
using StatsInterfaces;
using Unity.VisualScripting;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDummy : MonoBehaviour, IVulnerable//, ITargetable //그냥 임시 더미, 절대 이렇게 만들면 안 됨(IVulnerable의 구현이 여기서 왜 필요함)
{
	public float BasicHealth { get; private set; } = 100000f;
	public float MaxHealth { get; private set; } = 150000f;
	public double Health { get; private set; }

	public float BasicArmor { get; private set; } = 4000f;
	public float Armor { get; private set; }

	public bool IsDead { get; private set; }
	//[SerializeField] Transform myTransform;
	private float _armorIncreaseRate = 0f; //방어력 버프
	private Rigidbody2D _rb;
	private SpriteRenderer _sr;

	void Awake()
	{
		_rb = GetComponent<Rigidbody2D>();
		_sr = GetComponentInChildren<SpriteRenderer>();
		Health = MaxHealth;
		Armor = BasicArmor * (1 + _armorIncreaseRate);
		Debug.Log($"Enemy info: Health {Health}, Armor {Armor}");
	}
	void Update()
	{
		if (Health <= 0)
		{
			Debug.Log("I died");
			Destroy(this);
		}
		float tempArmor = BasicArmor * (1 + _armorIncreaseRate);
		if (!Mathf.Approximately(Armor, tempArmor))
		{
			Armor = tempArmor;
			Debug.Log($"Enemy info: Health {Health}, Armor {Armor}");
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
			double finalDamage = Math.Round(damage * mitigation * reduction);
			//Debug.Log($"Enemy has: 8000 / (8000 + {armor} * (1.0 - {ap})) = {mitigation}");
			Health = Math.Max(0D, Health - finalDamage);
	}
		//Debug.Log($"Enemy took {nowHealth - Health} damage and {Health} left");
		//_armorIncreaseRate += 0.1f;
	}
	public void TakeDamage(float damage, float apratio, StatsInterfaces.DamageType type)
	{
		if (type == StatsInterfaces.DamageType.Fixed)
		{
			Health -= damage;
		}
		else
		{
			//Health = Math.Max(0, Health - damage * 80 / (80 + Armor * (1 - apratio / 100))); // 대미지 * 피해율, 피해율 산출을 하나의 메서드로 사용? << PlayerStats의 ReduceStat에 구현됨
			_armorIncreaseRate += 0.1f; // 피격 시마다 방어력 10% 증가(임시 버프, 실제 버프는 이렇게 적용하지 않음)
			//StartCoroutine(Flash());
		}
		Debug.Log($"Enemy took {damage * 80 / (80 + Armor * (1 - apratio / 100))} damage");
		if (Health <= 0f) Die();
	}
	/*public bool TryGetTarget(out Transform target) {
		target = myTransform;
		return true;
	}*/
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
		Debug.Log("Now that's a LOTTA damage");
		IsDead = true;
		Destroy(gameObject);
	}
}
