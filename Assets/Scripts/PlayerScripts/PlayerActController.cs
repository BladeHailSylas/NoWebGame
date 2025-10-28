using ActInterfaces;
using StatsInterfaces;
using UnityEngine;

public class PlayerActController : IVulnerable, IPullable
{
	private PlayerStatsBridge _stats;
	private readonly FixedMotor _motor;
	private PlayerEffects _effects;

	public PlayerActController(FixedMotor motor, PlayerStatsBridge stats)//, BaseStatsContainer con)
	{
		_motor = motor;
		_stats = stats;

	}
	[Header("Move")]
	int _speed = 8;
	private readonly byte _mySId = 1; // = BattleCore.Manager.playerInfo.sid;
	// 입력 이벤트에서 방향만 갱신(즉시 이동 금지)
	public void MakeMove(FixedVector2 move, byte mySid = 1)
	{
		MakeMove(new NormalMoveData(move), mySid);
	}
	public void MakeMove(NormalMoveData move, byte mySid = 1)
	{
		_motor.Depenetrate();
		_motor.Move(move.Movement * (_speed));
		_motor.Depenetrate();
	}
	// --- IVulnerable ---
	public void TakeDamage(int damage, int apratio, DamageType type)
		=> _stats.ReduceStat(ReduceType.Health, damage, apratio, type);
	public void TakeDamage(float damage, float apratio, DamageType type)
	{
		TakeDamage((int)damage, (int)apratio, type);
	}

	public void Die()
	{
		
	}

	// --- IPullable ---
	public void ApplyKnockback(Vector2 direction, float force)
	{
		// Kinematic에서는 velocity/Force가 먹지 않으므로 Locomotion 버퍼로 위임
		//locomotion.ApplyKnockback(direction, force);
	}
}

public readonly struct BaseStatsContainer
{
	public readonly int BaseHp;
	public readonly int BaseHpGen;
	public readonly int BaseMana;
	public readonly int BaseManaGen;
	public readonly int BaseAttack;
	public readonly int BaseDefense;
	public readonly int BaseSpeed;

	public BaseStatsContainer(int bhp, int hpg, int bmp, int mpg, int bad, int bar, int bsp)
	{
		BaseHp = bhp;
		BaseHpGen = hpg;
		BaseMana = bmp;
		BaseManaGen = mpg;
		BaseAttack = bad;
		BaseDefense = bar;
		BaseSpeed = bsp;
	}
}