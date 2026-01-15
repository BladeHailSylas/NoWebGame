using System.Collections;
using System.Collections.Generic;
using Moves;
using PlayerScripts.Acts;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Systems.Data
{
	public interface IEntity : IVulnerable, IStackable
	{
	
	}

	public interface IStackable
	{
		public void ApplyStack(StackKey key, ushort tick = 0, int amount = 1, StackMetadata metadata = default);
		public void TryRemoveStack(SwitchVariable sv);
	}

	#region ===== Effect =====

	public enum EffectType
	{
		Stack = 0, Haste, DamageBoost, ArmorBoost, APBoost, DRBoost, Invisibility, Invincible, Slow, Stunned, Suppressed, Rooted, Tumbled, Damage //Damage는 지속 피해, duration을 0으로 하면 즉시 피해도 가능함
	}
	public class EffectState
	{
		public float duration;
		public int amplifier;
		public GameObject effecter;
		public string effectName;
		public EffectState()
		{ 
		}
		public EffectState(float dur, int amp, GameObject eft)
		{
			duration = dur; amplifier = amp; effecter = eft;
		}
		public EffectState(string name, int amp, GameObject eft)
		{
			duration = float.PositiveInfinity;
			effectName = name;
			amplifier = amp;
			effecter = eft;
		}
	}
	public interface IEffectStats
	{
		Dictionary<EffectType, EffectState> EffectList { get; }
		float EffectResistance { get; }
		bool HasEffect(EffectType e);
		HashSet<EffectType> PositiveEffects { get; }
		HashSet<EffectType> NegativeEffects { get; }
	}

	#endregion

	#region ===== Act =====

	public interface IVulnerable //피해를 받아 죽을 수 있음
	{
		void TakeDamage(DamageData data);
		void Die();
	}

	public interface IExpirable // 살아있는 시간이 제한된 엔터티, Expire는 Die가 아님
	{
		float Lifespan { get; }
		void Expire();
	}

	public interface IActivatable // 발동 가능한 행동(공격, 기술)
	{
		float BaseCooldown { get; }
		float MaxCooldown { get; }
		float Cooldown { get; }
	}

	public interface IAttackable : IActivatable // 일반 공격
	{
		void Attack(float attackDamage);
		//
		//void Drain(float amount); // 피흡, 생명력 흡수 스테이터스가 없을 경우에는?
	}

	public interface ICastable : IActivatable // 기술 캐스트
	{
		void Cast(CastKey key);
	}
	public enum CastKey
	{
		Skill1, Skill2, Skill3, Ultimate, General // Default Shift, Q, E, R, F
	}

	public interface ITogglable // 토글 기술
	{
		bool IsOn { get; }
		void Toggle();
	}
	
	public interface IDashable
	{
		public void AddDashContract(DashContract contract);
	}

	public interface ITeleportative
	{
		public void AddTeleportContract(TeleportContract tpc);
	}

	public interface IPullable
	{
		void ApplyKnockback(Vector2 direction, float force);
	}
	public interface ISweepable
	{
		FixedVector2 DepenVector(LayerMask blockersMask, int maxIterations = 4, float skin = 0.125f, float minEps = 0.001f, float maxTotal = 0.5f);
		void Move(FixedVector2 vec);
		int LastProcessedTick { get; }
	}
	public interface IMovable
	{
		Vector2 LastMoveDir { get; }
		void Move(Vector2 direction, Rigidbody2D rb, float velocity);
		//void Jump(float time, float wait = 1f);
	}

	public interface IAffectable
	{
		void ApplyEffect(EffectType buffType, GameObject effecter, float duration, int amplifier = 0, string name = null);
		void ApplyStack(string name, int amp, GameObject go);
		void Purify(EffectType buffType);
	}
	public interface ITargetable
	{
		bool TryGetTarget(out Transform target); // 잠금 대상이 없으면 false
	}

	public interface IVerboseDebugger
	{
		bool Verbose { get; }
	}

	#endregion

	#region ===== Stats =====

	public enum StatType
	{
		Health, HealthRegen,
		Armor, DamageReduction,
		Shield, SpecialShield,
		AttackDamage,
		Mana, ManaRegen,
		Speed, JumpTime
	}
	public enum StatRef
	{
		Base, Max, Current
	}
	public enum StatBool
	{
		OnGround, IsDead, IsImmune
	}
	public enum ReduceType
	{
		Health = 0, Mana
	}
	public enum DamageType
	{
		Normal, Fixed, CurrentPercent, LostPercent, MaxPercent
	}
	public interface IStatProvider
	{
		//float GetStat(StatType stat, StatRef re = StatRef.Current); -> stat이 모두 public get, private set이라 필요 없음
	}
/*public interface IDefensiveStats
	{
		float BaseHealth { get; }
		float MaxHealth { get; }
		float Health { get; }
		float BaseHealthRegen { get; }
		float HealthRegen { get; }

		float BaseArmor { get; }
		float Armor { get; }

	}
	
	public interface IResistiveStats
	{
		float Shield {  get; } // 일반 보호막
		float SpecialShield { get; } //특수 보호막은 일반 보호막과 다름
		float BaseDamageReduction { get; }
		float DamageReduction { get; }
	}

	public interface IOffensiveStats
	{
		float BaseAttackDamage { get; }
		float AttackDamage { get; }
		List<float> ArmorPenetration { get; }
	}
	public interface ICasterStats
	{
		float BaseMana { get; }
		float MaxMana { get; }
		float Mana { get; }
		float BaseManaRegen { get; }
		float ManaRegen { get; }
		//쿨타임?
	}

	public interface IMoverStats
	{
		bool OnGround { get; }
		float BaseVelocity { get; }
		float Velocity { get; }
		float JumpTime { get; }
	}*/

	#endregion

	#region ===== Skill =====

	public enum SkillSlot { Attack, AttackSkill, Skill1, Skill2, Ultimate }
	public interface ISkillParams { }                    // 파라미터 마커
	public interface ICooldownParams : ISkillParams { float Cooldown { get; } }
// 메커니즘(공식): "캐스팅 코루틴"을 제공
	[System.Obsolete]
	public interface ISkillMechanism
	{
		System.Type ParamType { get; }
		IEnumerator Execute(Transform owner, Camera cam, ISkillParams @params);
	}
	[System.Obsolete]
	public interface ITargetedMechanic : ISkillMechanism
	{
		IEnumerator Cast(Transform owner, Camera cam, ISkillParams @params, Transform target);
	}

// 제네릭 베이스: 타입 가드 + 제네릭 오버로드
	[System.Obsolete]
	public abstract class SkillMechanismBase<TParam> : ScriptableObject, ISkillMechanism
		where TParam : ISkillParams
	{
		public System.Type ParamType => typeof(TParam);

		public IEnumerator Execute(Transform owner, Camera cam, ISkillParams @params)
		{
			if (@params is not TParam p)
				throw new System.InvalidOperationException(
					$"Param type mismatch. Need {typeof(TParam).Name}, got {@params?.GetType().Name ?? "null"}");
			return Execute(owner, cam, p);
		}
		
		public abstract IEnumerator Execute(Transform owner, Camera cam, TParam param);
	}
	
	
	public interface IAnchorClearance
	{
		LayerMask WallsMask { get; }  // 벽/장애물
		float CollisionRadius { get; } // 스킬 충돌 반경(없으면 0)
		float AnchorSkin { get; }     // 벽 앞 여유(0.03~0.08)
	}
	public interface ITargetingData : IAnchorClearance
	{
		TargetMode Mode { get; }
		float FallbackRange { get; }  // FixedForward 거리
		Vector2 LocalOffset { get; }  // FixedOffset (TowardsCoordinate 모드에서 사용)
		LayerMask TargetMask { get; } // TowardsEntity 탐색 시 사용할 대상 마스크
		bool TargetSelf { get; }      // true일 경우 명시적으로 자신을 대상으로 삼습니다.
		bool CanPenetrate { get; } // 논타깃: 적중 시에도 종료되지 않는가, 타깃: 대상에게 적중하기 전까지 종료되지 않는가
	}

	#endregion
}