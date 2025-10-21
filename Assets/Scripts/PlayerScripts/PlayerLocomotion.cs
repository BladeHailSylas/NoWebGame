using Intents;

public class PlayerLocomotion
{
	public void CreateMoveIntent(FixedVector2 movement, byte mySid, ushort tick)
	{
		IntentCollector.Instance.QueueIntent(new MoveIntent(mySid, 0, tick, new NormalMoveData(movement)));
	}
	/*FixedVector2 _knockbackBudget;
		float distancePerTick = Mathf.Max(0f, force) / Ticker.TicksPerSecond;
		_knockbackBudget += new FixedVector2(dir * distancePerTick);
			delta += _knockbackBudget;
			_knockbackBudget = Vector2.zero;
		}
		// 의도 방향을 선호 방향으로 하여 겹침 청소(모서리 락 방지)
		var motor = GetComponentInParent<KinematicMotor2D>();
		if (!motor) return;
		//motor.RemoveComponent();
		motor.Depenetration();
		// 단일 스윕 이동(충돌로 절단/슬라이드는 Motor 정책에 따름)
		var res = motor.SweepMove(delta);
		motor.Depenetration();
		// 마지막 실제 이동 벡터 기록(원한다면 실제 속도 등 2차 파생 가능)
		LastMoveDir = direction;//motor.LastMoveVector;
	}

	/// <summary>
	/// IPullable: Kinematic에서는 velocity 변경이 무의미하므로,
	/// "즉시 한 번 밀리는 추가 변위" 예산으로 전환해 다음 Move에서 소비합니다.
	/// force 단위는 '거리'로 간주(필요 시 감쇠/시간기반으로 확장 가능).
	/// </summary>
	public void ApplyKnockback(Vector2 direction, float force)
	{
		Vector2 dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.zero;
		_knockbackBudget += dir * Mathf.Max(0f, force);
	}*/

	// (참고) 기존 Jump/Coroutine은 그대로 두되, 실제 수직 이동이 필요하면 별도 모터/레이어로 분리 권장 -> Jump를 계속 사용해야 할지 모르겠음
}