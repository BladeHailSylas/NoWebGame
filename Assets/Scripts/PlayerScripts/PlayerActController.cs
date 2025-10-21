using ActInterfaces;
using StatsInterfaces;
using UnityEngine;

public class PlayerActController : IVulnerable, IPullable
{
	private PlayerStats _stats;
	private readonly PlayerLocomotion _locomotion = new();
	private PlayerEffects _effects;
	public static PlayerActController Instance { get; private set; }

	public PlayerActController()
	{
		Instance ??= this;
	}
	[Header("Move")]
	int moveUnits = 8000;
	private readonly byte _mySId = 1; // = BattleCore.Manager.playerInfo.sid;
	// 입력 이벤트에서 방향만 갱신(즉시 이동 금지)
	public void MakeMove(FixedVector2 move, byte mySid = 1)
	{
		MakeMove(new NormalMoveData(move), mySid);
	}
	public void MakeMove(NormalMoveData move, byte mySid = 1)
	{
		if(mySid is 0 or > 6) mySid = _mySId;
		Debug.Log($"[MakeMove] frame={Time.frameCount} move={move.Type}");
		_locomotion.CreateMoveIntent(move.Movement * moveUnits, mySid, 0);
		
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
	public void TickPerformed(ushort tick = 0)
	{
		
	}

	// --- IPullable ---
	public void ApplyKnockback(Vector2 direction, float force)
	{
		// Kinematic에서는 velocity/Force가 먹지 않으므로 Locomotion 버퍼로 위임
		//locomotion.ApplyKnockback(direction, force);
	}
}