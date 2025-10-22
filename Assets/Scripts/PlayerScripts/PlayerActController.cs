using ActInterfaces;
using StatsInterfaces;
using UnityEngine;

public class PlayerActController : IVulnerable, IPullable
{
	private PlayerStats _stats;
	private readonly FixedMotor _motor;
	private PlayerEffects _effects;

	public PlayerActController(FixedMotor motor, Rigidbody2D rb)
	{
		_motor = motor;
	}
	[Header("Move")]
	int speed = 8;
	private readonly byte _mySId = 1; // = BattleCore.Manager.playerInfo.sid;
	// 입력 이벤트에서 방향만 갱신(즉시 이동 금지)
	public void MakeMove(FixedVector2 move, byte mySid = 1)
	{
		MakeMove(new NormalMoveData(move), mySid);
	}
	public void MakeMove(NormalMoveData move, byte mySid = 1)
	{
		_motor.Depenetrate();
		_motor.Move(move.Movement * (speed));
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